using FluentAssertions;
using Npgsql;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Black-box validation of the Phase 4 row-level security policies. Connects
/// to a real PostgreSQL instance via EDUCONNECT_TEST_DB_URL (see docker-compose
/// or a local test DB — migrations must already be applied) and verifies that
/// <c>app.current_school_id</c> confines every query to the matching tenant
/// without changing the caller's WHERE clause.
///
/// Requirements for green:
///   1. EDUCONNECT_TEST_DB_URL is set.
///   2. The migrations in this branch have been applied to that database.
///   3. The connection role DOES NOT have BYPASSRLS or SUPERUSER — both
///      skip RLS enforcement at the PG level regardless of FORCE. Tests
///      auto-skip (not fail) when this isn't the case, so running against
///      the default superuser role produces a clear Skip reason rather
///      than a misleading assertion failure.
///
/// Uses the subjects table because it's the simplest tenanted shape:
/// (id, school_id, name, created_at) with a unique (school_id, name) index.
/// </summary>
public class TenantIsolationRlsTests
{
    private const string NamePrefix = "rls-test";

    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("EDUCONNECT_TEST_DB_URL");

    [SkippableFact]
    public async Task Tenant_context_restricts_SELECT_to_matching_school()
    {
        await EnsureEnvironmentAsync();

        var (schoolA, schoolB, _, _) = await SeedTwoSchoolsAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await SetTenantAsync(conn, schoolA);

            var names = await QuerySubjectNamesAsync(conn);

            names.Should().HaveCount(1);
            names[0].Should().StartWith($"{NamePrefix}-A");
        }
        finally
        {
            await CleanupAsync(schoolA, schoolB);
        }
    }

    [SkippableFact]
    public async Task Tenant_context_blocks_cross_tenant_UPDATE()
    {
        await EnsureEnvironmentAsync();

        var (schoolA, schoolB, _, subjectB) = await SeedTwoSchoolsAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await SetTenantAsync(conn, schoolA);

            // UPDATE school B's subject by ID — without any WHERE on
            // school_id. RLS must scope the UPDATE to only rows visible
            // to school A, so zero rows are affected.
            await using var cmd = new NpgsqlCommand(
                "UPDATE subjects SET name = 'hijacked' WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", subjectB);
            var affected = await cmd.ExecuteNonQueryAsync();

            affected.Should().Be(0, "RLS must prevent cross-tenant UPDATE");
        }
        finally
        {
            await CleanupAsync(schoolA, schoolB);
        }
    }

    [SkippableFact]
    public async Task Tenant_context_blocks_INSERT_into_other_school()
    {
        await EnsureEnvironmentAsync();

        var (schoolA, schoolB, _, _) = await SeedTwoSchoolsAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await SetTenantAsync(conn, schoolA);

            // WITH CHECK must reject an INSERT whose school_id is school B
            // while the session is acting as school A.
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO subjects (id, school_id, name, created_at)
                VALUES (@id, @schoolB, @name, NOW())", conn);
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@schoolB", schoolB);
            cmd.Parameters.AddWithValue("@name", $"{NamePrefix}-smuggled-{Guid.NewGuid():N}");

            var act = async () => await cmd.ExecuteNonQueryAsync();
            await act.Should().ThrowAsync<PostgresException>()
                .Where(e => e.SqlState == "42501", "WITH CHECK violations surface as PG error 42501");
        }
        finally
        {
            await CleanupAsync(schoolA, schoolB);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static async Task EnsureEnvironmentAsync()
    {
        Skip.If(
            string.IsNullOrWhiteSpace(ConnectionString),
            "EDUCONNECT_TEST_DB_URL is not set; Phase 4 RLS tests skipped.");

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT rolbypassrls OR rolsuper FROM pg_roles WHERE rolname = current_user",
            conn);
        var bypass = await cmd.ExecuteScalarAsync();
        Skip.If(
            bypass is true,
            "Connection role has BYPASSRLS or SUPERUSER — RLS is not enforceable. " +
            "Create an app_runtime role (NOSUPERUSER NOBYPASSRLS) and re-point EDUCONNECT_TEST_DB_URL.");
    }

    private static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task SetTenantAsync(NpgsqlConnection conn, Guid schoolId)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT set_config('app.current_school_id', @id, false)", conn);
        cmd.Parameters.AddWithValue("@id", schoolId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ClearTenantAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT set_config('app.current_school_id', '', false)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> QuerySubjectNamesAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            $"SELECT name FROM subjects WHERE name LIKE '{NamePrefix}-%' ORDER BY name", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }
        return rows;
    }

    private static async Task<(Guid schoolA, Guid schoolB, Guid subjectA, Guid subjectB)>
        SeedTwoSchoolsAsync()
    {
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        var subjectA = Guid.NewGuid();
        var subjectB = Guid.NewGuid();

        var codeSuffixA = schoolA.ToString("N").Substring(0, 8);
        var codeSuffixB = schoolB.ToString("N").Substring(0, 8);

        await using var conn = await OpenConnectionAsync();
        await ClearTenantAsync(conn);

        await using (var cmd = new NpgsqlCommand(@"
            INSERT INTO schools (id, name, code, address, contact_phone, contact_email, created_at, updated_at)
            VALUES
              (@a, @aName, @aCode, '', '+810000000001', '', NOW(), NOW()),
              (@b, @bName, @bCode, '', '+810000000002', '', NOW(), NOW())", conn))
        {
            cmd.Parameters.AddWithValue("@a", schoolA);
            cmd.Parameters.AddWithValue("@aName", $"{NamePrefix}-A-{codeSuffixA}");
            cmd.Parameters.AddWithValue("@aCode", $"{NamePrefix}-A-{codeSuffixA}");
            cmd.Parameters.AddWithValue("@b", schoolB);
            cmd.Parameters.AddWithValue("@bName", $"{NamePrefix}-B-{codeSuffixB}");
            cmd.Parameters.AddWithValue("@bCode", $"{NamePrefix}-B-{codeSuffixB}");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(@"
            INSERT INTO subjects (id, school_id, name, created_at)
            VALUES
              (@sa, @a, @nameA, NOW()),
              (@sb, @b, @nameB, NOW())", conn))
        {
            cmd.Parameters.AddWithValue("@sa", subjectA);
            cmd.Parameters.AddWithValue("@a", schoolA);
            cmd.Parameters.AddWithValue("@nameA", $"{NamePrefix}-A-subject-{codeSuffixA}");
            cmd.Parameters.AddWithValue("@sb", subjectB);
            cmd.Parameters.AddWithValue("@b", schoolB);
            cmd.Parameters.AddWithValue("@nameB", $"{NamePrefix}-B-subject-{codeSuffixB}");
            await cmd.ExecuteNonQueryAsync();
        }

        return (schoolA, schoolB, subjectA, subjectB);
    }

    private static async Task CleanupAsync(Guid schoolA, Guid schoolB)
    {
        await using var conn = await OpenConnectionAsync();
        await ClearTenantAsync(conn);
        // Cascade from schools wipes subjects and any other tenant rows.
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM schools WHERE id IN (@a, @b)", conn);
        cmd.Parameters.AddWithValue("@a", schoolA);
        cmd.Parameters.AddWithValue("@b", schoolB);
        await cmd.ExecuteNonQueryAsync();
    }
}
