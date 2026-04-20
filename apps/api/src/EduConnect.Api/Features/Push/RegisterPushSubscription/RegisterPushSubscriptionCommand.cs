namespace EduConnect.Api.Features.Push.RegisterPushSubscription;

public record RegisterPushSubscriptionCommand(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent) : IRequest<RegisterPushSubscriptionResponse>;

public record RegisterPushSubscriptionResponse(Guid Id);
