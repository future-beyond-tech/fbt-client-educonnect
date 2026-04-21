namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Pluggable virus scanner for uploaded attachments. Production ships
/// <see cref="ClamAvAttachmentScanner"/>; development and CI fall back to
/// <see cref="NoOpAttachmentScanner"/> which passes every file.
/// </summary>
public interface IAttachmentScanner
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}

public sealed record ScanResult(ScanVerdict Verdict, string Engine, string? ThreatName = null)
{
    public bool IsClean => Verdict == ScanVerdict.Clean;
    public bool IsInfected => Verdict == ScanVerdict.Infected;
    public bool IsError => Verdict == ScanVerdict.Error;
}

public enum ScanVerdict
{
    Clean,
    Infected,
    Error,
}

public sealed class AttachmentScannerOptions
{
    public const string SectionName = "ClamAv";

    // When Enabled is false (the default for dev) the NoOp scanner is
    // registered, which lets local uploads flow end-to-end without
    // provisioning clamd. Production Railway deploys should set this to
    // true and point Host/Port at the clamd service.
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "clamav";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 30;
}
