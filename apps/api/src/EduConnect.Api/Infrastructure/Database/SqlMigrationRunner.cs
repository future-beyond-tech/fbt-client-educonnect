using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EduConnect.Api.Infrastructure.Database;

/// <summary>
/// Applies raw SQL migration files from Migrations/schema and Migrations/seed
/// on startup. Designed to be fully automatic — no human SQL steps required
/// on any environment, ever.
///
/// Key guarantees:
///   • Idempotent — every schema file is written to be safely re-runnable, and
///     the runner skips anything already recorded in schema_migrations.
///   • Auto-bootstrap — if the tracking table is missing but the database
///     already has schema (detected via a probe of a known table), the runner
///     backfills schema_migrations instead of re-running 001.
///   • Drift detection — every applied file has its SHA-256 stored in the
///     tracking table. On every startup the runner re-hashes files and refuses
///     to start if an already-applied file has been edited.
///   • Seed isolation — seed files only run when ASPNETCORE_ENVIRONMENT is
///     Development. Tracked in a separate seed_migrations table so schema and
///     seed can evolve independently.
///   • Multi-replica safe — a Postgres advisory lock serializes concurrent
///     boots of the app.
/// </summary>
public static class SqlMigrationRunner
{
    private const long AdvisoryLockKey = 7263548190123456789L;
    private const string MigrationsRoot = "Migrations";
    private const string SchemaSubfolder = "schema";
    private const string SeedSubfolder = "seed";
    private const string RunSeedsConfigKey = "EDUCONNECT_RUN_SEEDS";

    // A table that only exists if 001_foundation_tables has been applied.
    // Used by the bootstrap probe to decide whether an "untracked" DB is
    // actually empty (fresh) or already-migrated (legacy).
    private const string KnownTableProbe = "users";

    public static async Task ApplyAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, MigrationsRoot);
        var schemaDir = Path.Combine(baseDir, SchemaSubfolder);
        var seedDir = Path.Combine(baseDir, SeedSubfolder);

        if (!Directory.Exists(schemaDir))
        {
            throw new InvalidOperationException(
                $"Schema migrations directory not found at '{schemaDir}'. " +
                "Ensure Migrations/schema/*.sql files are copied to output.");
        }

        var schemaFiles = LoadMigrationFiles(schemaDir);
        var seedFiles = Directory.Exists(seedDir) ? LoadMigrationFiles(seedDir) : new List<MigrationFile>();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("AppDbContext has no connection string.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_lock({AdvisoryLockKey});", cancellationToken);

        try
        {
            // ── Schema track ──────────────────────────────────────────────
            await EnsureTrackingTableAsync(connection, "schema_migrations", cancellationToken);

            var schemaTableExisted = await WasTrackingTablePrePopulatedAsync(connection, "schema_migrations", cancellationToken);
            if (!schemaTableExisted)
            {
                // schema_migrations was just created by us. Decide: fresh DB, or legacy DB?
                var legacyDb = await TableExistsAsync(connection, KnownTableProbe, cancellationToken);
                if (legacyDb)
                {
                    logger.LogWarning(
                        "Detected existing schema without schema_migrations tracking — " +
                        "bootstrapping schema_migrations with all current files as already-applied.");
                    foreach (var file in schemaFiles)
                    {
                        await RecordMigrationAsync(connection, "schema_migrations", file.Filename, file.Checksum, cancellationToken);
                    }
                }
            }

            await ApplyTrackAsync(
                connection,
                schemaFiles,
                "schema_migrations",
                "schema",
                logger,
                cancellationToken);

            // ── Seed track (Development only) ─────────────────────────────
            var runSeeds = environment.IsDevelopment() || configuration.GetValue<bool>(RunSeedsConfigKey);
            if (runSeeds)
            {
                await EnsureTrackingTableAsync(connection, "seed_migrations", cancellationToken);
                await ApplyTrackAsync(
                    connection,
                    seedFiles,
                    "seed_migrations",
                    "seed",
                    logger,
                    cancellationToken);
            }
            else if (seedFiles.Count > 0)
            {
                logger.LogInformation(
                    "Skipping {Count} seed file(s) because environment is {Env} and {Key} is false",
                    seedFiles.Count, environment.EnvironmentName, RunSeedsConfigKey);
            }
        }
        finally
        {
            await ExecuteNonQueryAsync(
                connection,
                $"SELECT pg_advisory_unlock({AdvisoryLockKey});",
                CancellationToken.None);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Core apply loop, reused by both the schema and seed tracks.
    // ─────────────────────────────────────────────────────────────────────
    private static async Task ApplyTrackAsync(
        NpgsqlConnection connection,
        IReadOnlyList<MigrationFile> files,
        string trackingTable,
        string trackName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            logger.LogDebug("No {Track} migration files found", trackName);
            return;
        }

        var applied = await GetAppliedMigrationsAsync(connection, trackingTable, cancellationToken);

        // Drift detection: for every file already marked applied, verify the
        // checksum. If it differs, abort startup — someone edited history.
        var drifted = new List<string>();
        var backfill = new List<MigrationFile>();
        foreach (var file in files)
        {
            if (!applied.TryGetValue(file.Filename, out var storedChecksum))
            {
                continue;
            }

            if (storedChecksum is null)
            {
                // Legacy row from before the checksum column existed — backfill.
                backfill.Add(file);
            }
            else if (!string.Equals(storedChecksum, file.Checksum, StringComparison.Ordinal))
            {
                drifted.Add(file.Filename);
            }
        }

        if (drifted.Count > 0)
        {
            var list = string.Join(", ", drifted);
            throw new InvalidOperationException(
                $"Migration drift detected in {trackName}: {list}. " +
                "These files were modified after being applied. Revert the changes " +
                "or author a new migration to make the adjustment.");
        }

        foreach (var file in backfill)
        {
            await UpdateChecksumAsync(connection, trackingTable, file.Filename, file.Checksum, cancellationToken);
            logger.LogInformation("Backfilled checksum for {Track} migration {Filename}", trackName, file.Filename);
        }

        var pending = files.Where(f => !applied.ContainsKey(f.Filename)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("{Track} up to date ({Count} applied)", CapitalizeFirst(trackName), applied.Count);
            return;
        }

        logger.LogInformation("Applying {Count} pending {Track} migration(s)", pending.Count, trackName);

        foreach (var file in pending)
        {
            logger.LogInformation("Applying {Track} migration {Filename}", trackName, file.Filename);
            try
            {
                await ExecuteNonQueryAsync(connection, file.Sql, cancellationToken);
                await RecordMigrationAsync(connection, trackingTable, file.Filename, file.Checksum, cancellationToken);
                logger.LogInformation("Applied {Track} migration {Filename}", trackName, file.Filename);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply {Track} migration {Filename}. Halting startup.", trackName, file.Filename);
                throw;
            }
        }

        logger.LogInformation("All pending {Track} migrations applied successfully", trackName);
    }

    // ─────────────────────────────────────────────────────────────────────
    // File loading
    // ─────────────────────────────────────────────────────────────────────
    private static List<MigrationFile> LoadMigrationFiles(string directory)
    {
        return Directory.GetFiles(directory, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var bytes = File.ReadAllBytes(path);
                var sql = Encoding.UTF8.GetString(bytes);
                var checksum = ComputeSha256(bytes);
                return new MigrationFile(Path.GetFileName(path), sql, checksum);
            })
            .ToList();
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Tracking table plumbing
    // ─────────────────────────────────────────────────────────────────────
    private static async Task EnsureTrackingTableAsync(
        NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        // Create if missing.
        var createSql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                filename    TEXT PRIMARY KEY,
                checksum    TEXT,
                applied_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );";
        await ExecuteNonQueryAsync(connection, createSql, cancellationToken);

        // Add checksum column if upgrading from an older version of the runner
        // that didn't have one.
        var alterSql = $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS checksum TEXT;";
        await ExecuteNonQueryAsync(connection, alterSql, cancellationToken);
    }

    /// <summary>
    /// Returns true if the tracking table already contained rows before this
    /// startup — used to tell the difference between "we just created it" and
    /// "it was already there" for the bootstrap decision.
    /// </summary>
    private static async Task<bool> WasTrackingTablePrePopulatedAsync(
        NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand($"SELECT EXISTS(SELECT 1 FROM {tableName});", connection);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool b && b;
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name);",
            connection);
        cmd.Parameters.AddWithValue("name", tableName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool b && b;
    }

    private static async Task<Dictionary<string, string?>> GetAppliedMigrationsAsync(
        NpgsqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var applied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand($"SELECT filename, checksum FROM {tableName};", connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var filename = reader.GetString(0);
            var checksum = reader.IsDBNull(1) ? null : reader.GetString(1);
            applied[filename] = checksum;
        }
        return applied;
    }

    private static async Task RecordMigrationAsync(
        NpgsqlConnection connection,
        string tableName,
        string filename,
        string checksum,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            $@"INSERT INTO {tableName} (filename, checksum)
               VALUES (@filename, @checksum)
               ON CONFLICT (filename) DO UPDATE SET checksum = EXCLUDED.checksum;",
            connection);
        cmd.Parameters.AddWithValue("filename", filename);
        cmd.Parameters.AddWithValue("checksum", checksum);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateChecksumAsync(
        NpgsqlConnection connection,
        string tableName,
        string filename,
        string checksum,
        CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            $"UPDATE {tableName} SET checksum = @checksum WHERE filename = @filename;",
            connection);
        cmd.Parameters.AddWithValue("filename", filename);
        cmd.Parameters.AddWithValue("checksum", checksum);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = 300; // 5 minutes for large migrations
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private sealed record MigrationFile(string Filename, string Sql, string Checksum);
}
