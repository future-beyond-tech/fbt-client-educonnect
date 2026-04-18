using System.Text.Json;
using EduConnect.Api.Common.Models;

namespace EduConnect.Api.Common.Middleware;

/// <summary>
/// When an authenticated user still has must_change_password=true on their JWT,
/// every request is blocked with 403 MUST_CHANGE_PASSWORD except for a small
/// allow-list of endpoints needed to complete the forced change flow (logout,
/// refresh, identity, change-password/change-pin).
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MustChangePasswordMiddleware> _logger;

    // Endpoints the user is allowed to hit while must_change_password=true.
    // Anything outside this list is blocked.
    private static readonly string[] AllowedPaths =
    {
        "/health",
        "/api/auth/login",
        "/api/auth/login-parent",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/auth/me",
        "/api/auth/change-password",
        "/api/auth/change-pin",
        "/api/auth/forgot-password",
        "/api/auth/forgot-pin",
        "/api/auth/reset-password",
        "/api/auth/reset-pin"
    };

    public MustChangePasswordMiddleware(RequestDelegate next, ILogger<MustChangePasswordMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var claim = context.User.FindFirst("must_change_password")?.Value;
        var mustChange = string.Equals(claim, "true", StringComparison.OrdinalIgnoreCase);

        if (!mustChange)
        {
            await _next(context);
            return;
        }

        var isAllowed = AllowedPaths.Any(p =>
            context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

        if (isAllowed)
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Blocking request to {Path} — user must change their password/PIN before continuing.",
            context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        var problem = new ProblemDetailsResponse(
            StatusCodes.Status403Forbidden,
            "Password change required",
            "You must change your temporary password or PIN before using the app.",
            context.Request.Path,
            context.TraceIdentifier)
        {
            Errors = new Dictionary<string, string[]>
            {
                { "code", new[] { "MUST_CHANGE_PASSWORD" } }
            }
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
    }
}

public static class MustChangePasswordMiddlewareExtensions
{
    public static IApplicationBuilder UseMustChangePasswordEnforcement(this IApplicationBuilder builder)
        => builder.UseMiddleware<MustChangePasswordMiddleware>();
}
