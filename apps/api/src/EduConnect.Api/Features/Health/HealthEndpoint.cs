using System.Reflection;
using EduConnect.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Health;

public static class HealthEndpoint
{
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (AppDbContext db) =>
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "1.0.0";

            var uptime = DateTimeOffset.UtcNow - StartTime;

            var dbHealthy = false;
            var dbLatencyMs = -1L;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                dbHealthy = await db.Database.CanConnectAsync();
                sw.Stop();
                dbLatencyMs = sw.ElapsedMilliseconds;
            }
            catch
            {
                dbHealthy = false;
            }

            var status = dbHealthy ? "healthy" : "degraded";

            var response = new
            {
                status,
                version,
                uptime = new
                {
                    totalSeconds = (int)uptime.TotalSeconds,
                    formatted = $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s"
                },
                checks = new
                {
                    database = new
                    {
                        status = dbHealthy ? "connected" : "disconnected",
                        latencyMs = dbLatencyMs
                    }
                },
                timestamp = DateTimeOffset.UtcNow
            };

            return dbHealthy
                ? Results.Ok(response)
                : Results.Json(response, statusCode: 503);
        })
        .WithName("Health")
        .AllowAnonymous();
    }
}
