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

        // ?noRotate=true is used only by the Next.js Server Actions path
        // (see apps/web/docs/server-actions.md). It mints a fresh access
        // token without revoking the presented refresh token so concurrent
        // server actions don't trigger reuse-detection against themselves.
        var noRotate =
            string.Equals(context.Request.Query["noRotate"].ToString(), "true", StringComparison.OrdinalIgnoreCase);

        var result = await mediator.Send(new RefreshTokenCommand(noRotate), cancellationToken);

        if (!noRotate && result.NewRefreshToken is not null)
        {
            var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
            context.Response.Cookies.Append(
                "refresh_token",
                result.NewRefreshToken,
                RefreshTokenCookieOptions.Create(context.Request, refreshTokenExpiresAt));
        }

        return Results.Ok(new
        {
            accessToken = result.AccessToken,
            expiresIn = result.ExpiresIn,
            mustChangePassword = result.MustChangePassword
        });
    }
}
