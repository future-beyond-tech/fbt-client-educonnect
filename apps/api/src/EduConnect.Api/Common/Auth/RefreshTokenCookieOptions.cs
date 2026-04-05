using Microsoft.AspNetCore.Http;

namespace EduConnect.Api.Common.Auth;

public static class RefreshTokenCookieOptions
{
    public static CookieOptions Create(HttpRequest request, DateTimeOffset expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/"
        };
    }

    public static CookieOptions Delete(HttpRequest request)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };
    }
}
