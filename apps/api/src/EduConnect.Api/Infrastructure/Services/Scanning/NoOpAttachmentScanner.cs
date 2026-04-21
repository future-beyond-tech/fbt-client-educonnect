namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Dev/CI stand-in for a real virus scanner. Consumes the stream (so
/// callers can't tell the difference from a real scan) and returns Clean.
/// Logs at Warning so production never silently runs with this scanner
/// enabled without leaving evidence.
/// </summary>
public sealed class NoOpAttachmentScanner : IAttachmentScanner
{
    public const string EngineName = "noop";

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
            "NoOpAttachmentScanner is active — uploaded content was NOT scanned. " +
            "Set ClamAv:Enabled=true in production.");

        return new ScanResult(ScanVerdict.Clean, EngineName);
    }
}
