namespace EduConnect.Api.Features.Push.RegisterPushSubscription;

public static class RegisterPushSubscriptionEndpoint
{
    public static async Task<IResult> Handle(
        RegisterPushSubscriptionCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}
