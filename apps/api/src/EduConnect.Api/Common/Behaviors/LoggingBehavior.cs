using MediatR;
using EduConnect.Api.Common.Exceptions;

namespace EduConnect.Api.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var requestTypeName = typeof(TRequest).Name;

        try
        {
            var response = await next();
            stopwatch.Stop();

            _logger.LogInformation(
                "MediatR request {RequestType} completed successfully in {DurationMs}ms",
                requestTypeName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex) when (
            ex is EduConnect.Api.Common.Exceptions.ValidationException or
            NotFoundException or
            ForbiddenException or
            UnauthorizedException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "MediatR request {RequestType} failed with {ExceptionType} after {DurationMs}ms: {Message}",
                requestTypeName,
                ex.GetType().Name,
                stopwatch.ElapsedMilliseconds,
                ex.Message);
            throw;
        }
        catch
        {
            stopwatch.Stop();
            _logger.LogError(
                "MediatR request {RequestType} failed after {DurationMs}ms",
                requestTypeName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
