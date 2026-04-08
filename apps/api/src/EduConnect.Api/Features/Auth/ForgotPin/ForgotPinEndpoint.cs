using MediatR;

namespace EduConnect.Api.Features.Auth.ForgotPin;

public static class ForgotPinEndpoint
{
    public static async Task<IResult> Handle(
        ForgotPinCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new
        {
            message = "If a parent account exists for that email, a reset link has been sent."
        });
    }
}
