using MediatR;

namespace EduConnect.Api.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    public static async Task<IResult> Handle(
        ForgotPasswordCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new
        {
            message = "If an account exists for that email, a reset link has been sent."
        });
    }
}
