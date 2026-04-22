using System.Text.Json;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Models;
using FluentValidation;

namespace EduConnect.Api.Common.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var correlationId = context.Items.TryGetValue("CorrelationId", out var id) ? id?.ToString() : "N/A";
        var traceId = context.TraceIdentifier;

        var (statusCode, title, detail, errors) = MapException(exception);
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetailsResponse(statusCode, title, detail, context.Request.Path, traceId)
        {
            Errors = errors
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception,
                "Unhandled exception occurred | Status: {StatusCode} | CorrelationId: {CorrelationId} | TraceId: {TraceId}",
                statusCode, correlationId, traceId);
        }
        else
        {
            _logger.LogWarning(
                "Request failed | Status: {StatusCode} | Title: {Title} | Detail: {Detail} | CorrelationId: {CorrelationId} | TraceId: {TraceId}",
                statusCode, title, detail, correlationId, traceId);
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(problemDetails, options);

        return context.Response.WriteAsync(json);
    }

    private (int StatusCode, string Title, string Detail, Dictionary<string, string[]>? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            EduConnect.Api.Common.Exceptions.ValidationException validationEx => (400, "Validation Failed", "One or more validation errors occurred.", validationEx.Errors),
            NotFoundException notFoundEx => (404, "Not Found", notFoundEx.Message, null),
            ForbiddenException forbiddenEx => (403, "Forbidden", forbiddenEx.Message, null),
            UnauthorizedException unauthorizedEx => (401, "Unauthorized", unauthorizedEx.Message, null),
            // Storage backend (S3/R2/MinIO) failed — surface as 502 so the
            // caller knows it's an upstream issue, not a server-side bug.
            StorageException storageEx => (502, "Bad Gateway",
                _env.IsProduction() ? "Object storage is unavailable." : storageEx.Message, null),
            _ => (500, "Internal Server Error",
                _env.IsProduction() ? "An unexpected error occurred." : exception.Message, null)
        };
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalException(this IApplicationBuilder builder)
        => builder.UseMiddleware<GlobalExceptionMiddleware>();
}
