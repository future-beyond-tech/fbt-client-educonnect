using MediatR;

namespace EduConnect.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static async Task<IResult> Handle(
        LoginCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(command, cancellationToken);
        return Results.Ok(new
        {
            accessToken = response.AccessToken,
            expiresIn = response.ExpiresIn,
            mustChangePassword = response.MustChangePassword
        });
    }
}
