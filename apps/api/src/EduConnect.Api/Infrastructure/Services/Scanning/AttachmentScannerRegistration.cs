using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// DI wiring for the attachment virus scanner. Extracted from Program.cs so
/// the production-environment guard can be unit-tested without booting a
/// full WebApplication.
/// </summary>
public static class AttachmentScannerRegistration
{
    /// <summary>
    /// Registers <see cref="IAttachmentScanner"/> based on the supplied
    /// options and hosting environment.
    ///
    /// Production must have <c>ClamAv:Enabled=true</c>. If ClamAV is
    /// disabled in Production the method throws on startup — shipping
    /// without a real scanner is treated as a configuration error, not a
    /// soft-fallback, because the NoOp scanner fails every upload closed.
    /// </summary>
    public static IServiceCollection AddAttachmentScanner(
        this IServiceCollection services,
        AttachmentScannerOptions options,
        IHostEnvironment environment)
    {
        if (options.Enabled)
        {
            services.AddSingleton<IAttachmentScanner, ClamAvAttachmentScanner>();
            return services;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "CLAMAV_ENABLED must be true in Production. " +
                "NoOpAttachmentScanner is not permitted in production environments.");
        }

        services.AddSingleton<IAttachmentScanner, NoOpAttachmentScanner>();
        // Surface the fail-closed posture on every startup so operators in
        // dev/staging don't silently wonder why every parent sees zero
        // attachments. Admins get the same hint surfaced in the attachment
        // list DTO via AttachmentDto.ScanFailureReason.
        services.AddHostedService<NoOpScannerStartupWarning>();
        return services;
    }
}

internal sealed class NoOpScannerStartupWarning : IHostedService
{
    private readonly ILogger<NoOpScannerStartupWarning> _logger;

    public NoOpScannerStartupWarning(ILogger<NoOpScannerStartupWarning> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "NoOpAttachmentScanner is active — every uploaded attachment will be marked ScanFailed " +
            "and will not be visible to non-admin users. Set CLAMAV_ENABLED=true (with CLAMAV_HOST / " +
            "CLAMAV_PORT) to enable real virus scanning.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
