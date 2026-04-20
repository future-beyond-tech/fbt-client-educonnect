using Microsoft.AspNetCore.Mvc;

namespace EduConnect.Api.Features.Push.UnregisterPushSubscription;

public static class UnregisterPushSubscriptionEndpoint
{
    public static async Task<IResult> Handle(
        [FromQuery] string endpoint,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UnregisterPushSubscriptionCommand(endpoint), cancellationToken);
        return Results.NoContent();
    }
}
