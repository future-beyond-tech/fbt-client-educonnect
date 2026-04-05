using EduConnect.Api.Common.Auth;
using MediatR;

namespace EduConnect.Api.Features.Auth.Logout;

public static class LogoutEndpoint
{
    public static async Task<IResult> Handle(
        CurrentUserService currentUserService,
        IMediator mediator,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(currentUserService.UserId);
        await mediator.Send(command, cancellationToken);

        context.Response.Cookies.Delete("refresh_token", RefreshTokenCookieOptions.Delete(context.Request));

        return Results.NoContent();
    }
}
