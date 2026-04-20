namespace EduConnect.Api.Infrastructure.Services;

public record PushPayload(
    string Title,
    string? Body,
    string? Url,
    string Type,
    Guid? EntityId,
    string? EntityType);

public interface IPushSender
{
    /// <summary>
    /// Deliver a push payload to every active subscription for the given users.
    /// Must not throw — push failures are logged, stale subscriptions removed.
    /// </summary>
    Task FanOutAsync(
        IReadOnlyList<Guid> userIds,
        PushPayload payload,
        CancellationToken cancellationToken = default);
}
