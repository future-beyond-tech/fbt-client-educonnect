using MediatR;

namespace EduConnect.Api.Features.Auth.ChangePassword;

public static class ChangePasswordEndpoint
{
    public static async Task<IResult> Handle(
        ChangePasswordCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.Ok(new { message = "Password changed successfully. Please log in again." });
    }
}
