namespace EduConnect.Api.Features.Auth.RefreshToken;

/// <summary>
/// <paramref name="NoRotate"/> tells the handler to mint a fresh access
/// token WITHOUT revoking the presented refresh token or issuing a new
/// one. Used only by the Next.js Server Actions path (Phase 7), where
/// concurrent actions presenting the same cookie would otherwise trigger
/// reuse-detection and log the user out. See apps/web/docs/server-actions.md.
/// </summary>
public record RefreshTokenCommand(bool NoRotate = false) : IRequest<RefreshTokenResponse>;

public record RefreshTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string? NewRefreshToken,
    bool MustChangePassword);
