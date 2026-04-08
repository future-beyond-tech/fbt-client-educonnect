using MediatR;

namespace EduConnect.Api.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static async Task<IResult> Handle(
        ResetPasswordCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new { message = "Password reset successfully. Please log in with your new password." });
    }
}
