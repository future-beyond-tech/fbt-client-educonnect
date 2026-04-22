using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Tells tenant admins when an upload was blocked. Called by the scan
/// worker after a row transitions to Infected or ScanFailed. Failures
/// are absorbed inside the implementation so the primary status update
/// never gets undone by a side-channel error.
/// </summary>
public interface IAttachmentBlockedNotifier
{
    Task NotifyAsync(
        AttachmentBlockedKind kind,
        AttachmentEntity attachment,
        CancellationToken cancellationToken = default);
}

public enum AttachmentBlockedKind
{
    /// <summary>Scanner found a threat. Object deleted from storage.</summary>
    Infected,

    /// <summary>Scanner failed after retries. Object kept for operator review.</summary>
    ScanFailed,
}

/// <summary>
/// Wire-format type strings persisted on <c>notifications.type</c>. Kept
/// in sync with the chk_notification_type CHECK constraint (see
/// <c>NotificationConfiguration</c> + the
/// <c>ExpandNotificationTypesForAttachments</c> migration).
/// </summary>
public static class AttachmentNotificationTypes
{
    public const string Infected = "attachment_infected";
    public const string ScanFailed = "attachment_scan_failed";
    public const string EntityType = "attachment";
}
