using EduConnect.Api.Common.Auth;
using MediatR;

namespace EduConnect.Api.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Cookies.ContainsKey("refresh_token"))
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new RefreshTokenCommand(), cancellationToken);
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);

        context.Response.Cookies.Append(
            "refresh_token",
            result.NewRefreshToken,
            RefreshTokenCookieOptions.Create(context.Request, refreshTokenExpiresAt));

        return Results.Ok(new
        {
            accessToken = result.AccessToken,
            mustChangePassword = result.MustChangePassword
        });
    }
}
