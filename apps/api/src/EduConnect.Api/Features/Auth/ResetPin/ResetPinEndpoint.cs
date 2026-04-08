using MediatR;

namespace EduConnect.Api.Features.Auth.ResetPin;

public static class ResetPinEndpoint
{
    public static async Task<IResult> Handle(
        ResetPinCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new { message = "PIN reset successfully. Please log in with your new PIN." });
    }
}
