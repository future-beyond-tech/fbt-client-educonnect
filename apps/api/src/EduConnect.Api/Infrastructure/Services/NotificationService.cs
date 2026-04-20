using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.Extensions.Logging;

namespace EduConnect.Api.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IPushSender _pushSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext context,
        IPushSender pushSender,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task SendAsync(
        Guid schoolId,
        Guid userId,
        string type,
        string title,
        string? body,
        Guid? entityId,
        string? entityType,
        CancellationToken cancellationToken = default)
    {
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            EntityId = entityId,
            EntityType = entityType,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notification sent: {NotificationId} type={Type} to user {UserId}",
            notification.Id, type, userId);

        await FanOutPushSafelyAsync(new[] { userId }, type, title, body, entityId, entityType, cancellationToken);
    }

    public async Task SendBatchAsync(
        Guid schoolId,
        IReadOnlyList<Guid> userIds,
        string type,
        string title,
        string? body,
        Guid? entityId,
        string? entityType,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var distinctUserIds = userIds.Distinct().ToList();

        var notifications = distinctUserIds.Select(uid => new NotificationEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = uid,
            Type = type,
            Title = title,
            Body = body,
            EntityId = entityId,
            EntityType = entityType,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Batch notification sent: type={Type} to {Count} users for entity {EntityId}",
            type, distinctUserIds.Count, entityId);

        await FanOutPushSafelyAsync(distinctUserIds, type, title, body, entityId, entityType, cancellationToken);
    }

    private async Task FanOutPushSafelyAsync(
        IReadOnlyList<Guid> userIds,
        string type,
        string title,
        string? body,
        Guid? entityId,
        string? entityType,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = new PushPayload(
                Title: title,
                Body: body,
                Url: BuildDeepLink(type, entityId, entityType),
                Type: type,
                EntityId: entityId,
                EntityType: entityType);

            await _pushSender.FanOutAsync(userIds, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            // Push is a secondary channel — a failure here must never cause
            // the primary DB notification to appear undelivered.
            _logger.LogError(ex,
                "Push fan-out failed for type={Type} userCount={Count}",
                type, userIds.Count);
        }
    }

    /// <summary>
    /// Build a relative URL the service worker can use to deep-link into the
    /// web app when the user taps the notification. Kept as relative paths so
    /// it works across environments.
    /// </summary>
    private static string? BuildDeepLink(string type, Guid? entityId, string? entityType)
    {
        return (entityType, entityId) switch
        {
            ("notice", { } id) => $"/notices/{id}",
            ("homework", { } id) => $"/homework/{id}",
            ("attendance", _) => "/attendance",
            ("leave_application", { } id) => $"/leaves/{id}",
            _ => "/notifications",
        };
    }
}
