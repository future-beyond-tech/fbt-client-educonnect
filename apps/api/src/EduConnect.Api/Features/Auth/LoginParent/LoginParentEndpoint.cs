using MediatR;

namespace EduConnect.Api.Features.Auth.LoginParent;

public static class LoginParentEndpoint
{
    public static async Task<IResult> Handle(
        LoginParentCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(command, cancellationToken);
        return Results.Ok(new { accessToken = response.AccessToken });
    }
}
