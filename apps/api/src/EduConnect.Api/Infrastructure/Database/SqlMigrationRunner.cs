using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EduConnect.Api.Infrastructure.Database;

/// <summary>
/// Applies raw SQL migration files from the Migrations/ folder on startup.
/// Uses a schema_migrations tracking table and a Postgres advisory lock so it
/// is safe to run from multiple replicas concurrently.
/// </summary>
public static class SqlMigrationRunner
{
    private const long AdvisoryLockKey = 7263548190123456789L;
    private const string MigrationsFolderName = "Migrations";

    public static async Task ApplyAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, MigrationsFolderName);
        if (!Directory.Exists(migrationsDir))
        {
            throw new InvalidOperationException(
                $"SQL migrations directory not found at '{migrationsDir}'. Ensure *.sql files are copied to output.");
        }

        var files = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            logger.LogWarning("No SQL migration files found in {Dir}", migrationsDir);
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("AppDbContext has no connection string.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Acquire a session-level advisory lock so concurrent app instances
        // don't race on first deploy / cold start.
        await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_lock({AdvisoryLockKey});", cancellationToken);

        try
        {
            await EnsureTrackingTableAsync(connection, cancellationToken);

            var applied = await GetAppliedMigrationsAsync(connection, cancellationToken);
            var pending = files
                .Where(f => !applied.Contains(Path.GetFileName(f)))
                .ToList();

            if (pending.Count == 0)
            {
                logger.LogInformation("Database schema up to date ({Count} migrations applied)", applied.Count);
                return;
            }

            logger.LogInformation("Applying {Count} pending SQL migration(s)", pending.Count);

            foreach (var file in pending)
            {
                var filename = Path.GetFileName(file);
                var sql = await File.ReadAllTextAsync(file, cancellationToken);

                logger.LogInformation("Applying migration {Filename}", filename);

                try
                {
                    await ExecuteNonQueryAsync(connection, sql, cancellationToken);
                    await RecordMigrationAsync(connection, filename, cancellationToken);
                    logger.LogInformation("Applied migration {Filename}", filename);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to apply migration {Filename}. Halting startup.", filename);
                    throw;
                }
            }

            logger.LogInformation("All pending migrations applied successfully");
        }
        finally
        {
            await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_unlock({AdvisoryLockKey});", CancellationToken.None);
        }
    }

    private static async Task EnsureTrackingTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                filename    TEXT PRIMARY KEY,
                applied_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );";
        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
    }

    private static async Task<HashSet<string>> GetAppliedMigrationsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand("SELECT filename FROM schema_migrations;", connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }
        return applied;
    }

    private static async Task RecordMigrationAsync(NpgsqlConnection connection, string filename, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO schema_migrations (filename) VALUES (@filename) ON CONFLICT (filename) DO NOTHING;",
            connection);
        cmd.Parameters.AddWithValue("filename", filename);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = 300; // 5 minutes for large migrations
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
