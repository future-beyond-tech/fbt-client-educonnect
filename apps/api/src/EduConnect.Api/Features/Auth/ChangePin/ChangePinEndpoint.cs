using MediatR;

namespace EduConnect.Api.Features.Auth.ChangePin;

public static class ChangePinEndpoint
{
    public static async Task<IResult> Handle(
        ChangePinCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new { message = "PIN changed successfully. Please log in again." });
    }
}
