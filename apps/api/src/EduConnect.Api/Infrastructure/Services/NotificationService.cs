using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.Extensions.Logging;

namespace EduConnect.Api.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
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
    }
}
