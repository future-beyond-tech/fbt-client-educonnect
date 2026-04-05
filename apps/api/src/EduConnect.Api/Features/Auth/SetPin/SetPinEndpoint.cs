using MediatR;

namespace EduConnect.Api.Features.Auth.SetPin;

public static class SetPinEndpoint
{
    public static async Task<IResult> Handle(
        SetPinCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new { message = "PIN set successfully." });
    }
}
