using System.Data.Common;
using EduConnect.Api.Common.Auth;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EduConnect.Api.Infrastructure.Database.Interceptors;

/// <summary>
/// On every physical connection open, stamps <c>app.current_school_id</c> on
/// the PostgreSQL session so the per-table RLS policy
/// <c>current_app_school_id() IS NULL OR school_id = current_app_school_id()</c>
/// evaluates to the caller's tenant only.
///
/// When no tenant is known (anonymous login/refresh paths, startup seeding),
/// the GUC is explicitly cleared to empty so the policy function returns NULL
/// and the row is visible — matching the existing EF global query filter
/// semantics where <c>!IsAuthenticated</c> short-circuits the tenant check.
/// Anonymous code paths are a hand-curated surface; app-level controls
/// continue to gate which tables they touch.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly CurrentUserService _currentUserService;

    public TenantConnectionInterceptor(CurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantGucAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        ApplyTenantGuc(connection);
        base.ConnectionOpened(connection, eventData);
    }

    private async Task ApplyTenantGucAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_school_id', @p0, false)";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = ResolveTenantValue();
        cmd.Parameters.Add(parameter);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void ApplyTenantGuc(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_school_id', @p0, false)";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = ResolveTenantValue();
        cmd.Parameters.Add(parameter);
        cmd.ExecuteNonQuery();
    }

    private string ResolveTenantValue()
    {
        if (_currentUserService.IsAuthenticated && _currentUserService.SchoolId != Guid.Empty)
        {
            return _currentUserService.SchoolId.ToString();
        }
        // Empty string → current_app_school_id() returns NULL → policy bypasses
        // tenant check. Only reachable on anonymous/bootstrap paths.
        return string.Empty;
    }
}
