namespace EduConnect.Api.Infrastructure.Services;

public interface INotificationService
{
    /// <summary>
    /// Sends a notification to a single user.
    /// </summary>
    Task SendAsync(
        Guid schoolId,
        Guid userId,
        string type,
        string title,
        string? body,
        Guid? entityId,
        string? entityType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the same notification to multiple users in a single batch.
    /// </summary>
    Task SendBatchAsync(
        Guid schoolId,
        IReadOnlyList<Guid> userIds,
        string type,
        string title,
        string? body,
        Guid? entityId,
        string? entityType,
        CancellationToken cancellationToken = default);
}
