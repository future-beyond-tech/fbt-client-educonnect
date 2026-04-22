using System.Net;
using System.Text;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

public sealed class AttachmentBlockedNotifier : IAttachmentBlockedNotifier
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AttachmentBlockedNotifier> _logger;

    public AttachmentBlockedNotifier(
        AppDbContext db,
        INotificationService notificationService,
        IEmailService emailService,
        ILogger<AttachmentBlockedNotifier> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task NotifyAsync(
        AttachmentBlockedKind kind,
        AttachmentEntity attachment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Worker doesn't carry tenant context — bypass the global query
            // filter to find tenant admins, then fan out per admin so each
            // recipient gets their own notification row.
            var admins = await _db.Users
                .IgnoreQueryFilters()
                .Where(u =>
                    u.SchoolId == attachment.SchoolId &&
                    u.Role == "Admin" &&
                    u.IsActive)
                .Select(u => new { u.Id, u.Email, u.Name })
                .ToListAsync(cancellationToken);

            if (admins.Count == 0)
            {
                _logger.LogWarning(
                    "No active admins for school {SchoolId}; skipping attachment-blocked notification for {AttachmentId}",
                    attachment.SchoolId, attachment.Id);
                return;
            }

            var (type, title, body) = BuildContent(kind, attachment);

            await _notificationService.SendBatchAsync(
                attachment.SchoolId,
                admins.Select(a => a.Id).ToList(),
                type,
                title,
                body,
                attachment.Id,
                AttachmentNotificationTypes.EntityType,
                cancellationToken);

            // Email is best-effort — already-sent in-app notifications stand
            // even if SMTP is unhappy.
            var subject = title;
            var htmlBody = BuildEmailHtml(kind, attachment, body);
            foreach (var admin in admins)
            {
                if (string.IsNullOrWhiteSpace(admin.Email)) continue;

                try
                {
                    await _emailService.SendEmailAsync(
                        admin.Email!,
                        subject,
                        htmlBody,
                        textBody: body,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to email attachment-blocked notice to admin {AdminId} for {AttachmentId}",
                        admin.Id, attachment.Id);
                }
            }
        }
        catch (Exception ex)
        {
            // Notification dispatch is a side-effect; never let it bubble
            // up and undo the worker's primary status write.
            _logger.LogError(ex,
                "AttachmentBlockedNotifier failed for attachment {AttachmentId}",
                attachment.Id);
        }
    }

    private static (string Type, string Title, string Body) BuildContent(
        AttachmentBlockedKind kind, AttachmentEntity attachment)
    {
        var fileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "(unnamed)" : attachment.FileName;
        var threat = string.IsNullOrWhiteSpace(attachment.ThreatName) ? "unknown" : attachment.ThreatName!;

        return kind switch
        {
            AttachmentBlockedKind.Infected => (
                AttachmentNotificationTypes.Infected,
                $"Upload blocked: {fileName}",
                $"The virus scanner flagged \"{fileName}\" as {threat}. The file has been removed from storage."
            ),
            AttachmentBlockedKind.ScanFailed => (
                AttachmentNotificationTypes.ScanFailed,
                $"Scan failed: {fileName}",
                $"The virus scanner could not check \"{fileName}\" ({threat}). The file is held for operator review."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blocked kind"),
        };
    }

    private static string BuildEmailHtml(
        AttachmentBlockedKind kind, AttachmentEntity attachment, string body)
    {
        // Deliberately barebones — no shared EmailLayout template. This is
        // an operator alert, not a branded user-facing email; clarity beats
        // visual polish.
        var sb = new StringBuilder();
        sb.Append("<p>");
        sb.Append(WebUtility.HtmlEncode(body));
        sb.Append("</p>");
        sb.Append("<ul>");
        sb.Append($"<li><strong>Attachment ID:</strong> {WebUtility.HtmlEncode(attachment.Id.ToString())}</li>");
        if (attachment.EntityType is not null)
        {
            sb.Append($"<li><strong>Linked to:</strong> {WebUtility.HtmlEncode(attachment.EntityType)} ");
            sb.Append($"{WebUtility.HtmlEncode(attachment.EntityId?.ToString() ?? "(unattached)")}</li>");
        }
        sb.Append($"<li><strong>Uploaded by:</strong> {WebUtility.HtmlEncode(attachment.UploadedById.ToString())}</li>");
        sb.Append("</ul>");
        sb.Append("<p>No action is required for ");
        sb.Append(kind == AttachmentBlockedKind.Infected ? "an infected upload" : "a scan failure");
        sb.Append(" beyond auditing the source. See the EduConnect admin console for the full attachment list.</p>");
        return sb.ToString();
    }
}
