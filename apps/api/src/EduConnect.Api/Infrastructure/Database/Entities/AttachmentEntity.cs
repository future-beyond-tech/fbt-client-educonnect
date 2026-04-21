namespace EduConnect.Api.Infrastructure.Database.Entities;

public class AttachmentEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public Guid UploadedById { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    // Phase 5 — virus-scan state machine.
    // Rows created before this phase are backfilled to Available.
    public string Status { get; set; } = AttachmentStatus.Pending;
    public DateTimeOffset? ScannedAt { get; set; }
    public string? ThreatName { get; set; }

    public SchoolEntity? School { get; set; }
    public UserEntity? UploadedBy { get; set; }
}

public static class AttachmentStatus
{
    // Just uploaded; still in the scan queue or being scanned.
    public const string Pending = "Pending";

    // Scanned clean and safe to hand out a download URL for.
    public const string Available = "Available";

    // Scan found a threat; the object has been deleted from storage.
    public const string Infected = "Infected";

    // Scanner errored after retries; the object is still in storage but not
    // served. Operator intervention may re-queue or delete.
    public const string ScanFailed = "ScanFailed";

    public static readonly string[] All = { Pending, Available, Infected, ScanFailed };
}
