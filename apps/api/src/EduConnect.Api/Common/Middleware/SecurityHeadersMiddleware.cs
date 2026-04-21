namespace EduConnect.Api.Common.Middleware;

// Emits defence-in-depth response headers for every API response.
// The API returns JSON only, so CSP is minimal ('none' + no framing).
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            var headers = ctx.Response.Headers;

            static void SetIfMissing(IHeaderDictionary headers, string name, string value)
            {
                if (!headers.ContainsKey(name))
                {
                    headers[name] = value;
                }
            }

            SetIfMissing(headers, "Strict-Transport-Security", "max-age=63072000; includeSubDomains; preload");
            SetIfMissing(headers, "X-Content-Type-Options", "nosniff");
            SetIfMissing(headers, "X-Frame-Options", "DENY");
            SetIfMissing(headers, "Referrer-Policy", "strict-origin-when-cross-origin");
            SetIfMissing(headers, "Permissions-Policy", "camera=(), microphone=(), geolocation=(), interest-cohort=()");
            SetIfMissing(headers, "Cross-Origin-Resource-Policy", "same-origin");
            SetIfMissing(headers, "Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");

            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        }, context);

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
