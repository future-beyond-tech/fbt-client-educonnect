namespace EduConnect.Api.Common.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        var correlationId = context.Items.TryGetValue("CorrelationId", out var id) ? id?.ToString() : "N/A";
        var statusCode = context.Response.StatusCode;
        var durationMs = stopwatch.ElapsedMilliseconds;

        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        if (statusCode == StatusCodes.Status401Unauthorized &&
            context.Request.Path.StartsWithSegments("/api/auth/refresh", StringComparison.OrdinalIgnoreCase))
        {
            logLevel = LogLevel.Information;
        }

        _logger.Log(logLevel,
            "HTTP {Method} {Path} completed with status {StatusCode} in {DurationMs}ms | CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            statusCode,
            durationMs,
            correlationId);
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        => builder.UseMiddleware<RequestLoggingMiddleware>();
}
