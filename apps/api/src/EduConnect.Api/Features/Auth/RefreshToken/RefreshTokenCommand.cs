namespace EduConnect.Api.Features.Auth.RefreshToken;

public record RefreshTokenCommand : IRequest<RefreshTokenResponse>;

public record RefreshTokenResponse(string AccessToken, string NewRefreshToken, bool MustChangePassword);
