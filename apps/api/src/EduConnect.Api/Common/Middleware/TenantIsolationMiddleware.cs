using System.Security.Claims;
using EduConnect.Api.Common.Auth;
using Serilog.Context;

namespace EduConnect.Api.Common.Middleware;

public class TenantIsolationMiddleware
{
    private readonly RequestDelegate _next;

    public TenantIsolationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CurrentUserService currentUserService)
    {
        var unauthenticatedEndpoints = new[]
        {
            "/health",
            "/api/auth/login",
            "/api/auth/login-parent",
            "/api/auth/refresh"
        };

        var isUnauthenticatedEndpoint = unauthenticatedEndpoints.Any(ep =>
            context.Request.Path.StartsWithSegments(ep, StringComparison.OrdinalIgnoreCase));

        if (!isUnauthenticatedEndpoint && context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            var schoolIdClaim = context.User.FindFirst("schoolId")?.Value;
            var roleClaim =
                context.User.FindFirst("role")?.Value ??
                context.User.FindFirst(ClaimTypes.Role)?.Value;
            var nameClaim =
                context.User.FindFirst("name")?.Value ??
                context.User.FindFirst(ClaimTypes.Name)?.Value;

            if (Guid.TryParse(userIdClaim, out var userId) && Guid.TryParse(schoolIdClaim, out var schoolId))
            {
                currentUserService.UserId = userId;
                currentUserService.SchoolId = schoolId;
                currentUserService.Role = roleClaim ?? string.Empty;
                currentUserService.Name = nameClaim ?? string.Empty;
            }
        }

        var isAuthenticated = currentUserService.IsAuthenticated;
        using (LogContext.PushProperty("UserId", isAuthenticated ? currentUserService.UserId : "anonymous"))
        using (LogContext.PushProperty("SchoolId", isAuthenticated ? currentUserService.SchoolId : "unknown"))
        using (LogContext.PushProperty("Role", isAuthenticated ? currentUserService.Role : "anonymous"))
        {
            await _next(context);
        }
    }
}

public static class TenantIsolationMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantIsolation(this IApplicationBuilder builder)
        => builder.UseMiddleware<TenantIsolationMiddleware>();
}
