namespace EduConnect.Api.Features.Push.UnregisterPushSubscription;

public record UnregisterPushSubscriptionCommand(string Endpoint) : IRequest<Unit>;
