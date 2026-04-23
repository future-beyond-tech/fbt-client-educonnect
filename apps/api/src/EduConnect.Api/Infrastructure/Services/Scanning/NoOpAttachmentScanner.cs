namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Dev/CI stand-in for a real virus scanner. Drains the stream (so callers
/// can't distinguish it from a real scan) and returns a Clean verdict.
///
/// Fail-open (DEV ONLY): every file routed through this scanner lands in
/// <c>AttachmentStatus.Available</c>. This avoids the need to run the heavy
/// clamd container during local development on arm64 architectures.
/// Startup registration refuses to wire this scanner in a Production 
/// environment — see <c>AttachmentScannerRegistration</c>.
/// </summary>
public sealed class NoOpAttachmentScanner : IAttachmentScanner
{
    public const string EngineName = "noop";

    // Surfaces on the attachment row's ThreatName column and in logs so
    // operators can distinguish a real ClamAV error from a dev-environment
    // misconfiguration.
    public const string NoOpThreatName = "NOOP_SCANNER_DISABLED";

    private readonly ILogger<NoOpAttachmentScanner> _logger;

    public NoOpAttachmentScanner(ILogger<NoOpAttachmentScanner> logger)
    {
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[64 * 1024];
        while (await content.ReadAsync(buffer, cancellationToken) > 0)
        {
            // Drain the stream.
        }

        _logger.LogWarning(
            "NoOpAttachmentScanner is active — no virus scan performed. " +
            "File will be marked Available (Dev Mode). Set ClamAv:Enabled=true to use real virus scanning.");

        return new ScanResult(ScanVerdict.Clean, EngineName, null);
    }
}
