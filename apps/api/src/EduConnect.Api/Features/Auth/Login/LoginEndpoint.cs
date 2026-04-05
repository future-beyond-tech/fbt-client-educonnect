using MediatR;

namespace EduConnect.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static async Task<IResult> Handle(
        LoginCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var accessToken = await mediator.Send(command, cancellationToken);
        return Results.Ok(new { accessToken });
    }
}
