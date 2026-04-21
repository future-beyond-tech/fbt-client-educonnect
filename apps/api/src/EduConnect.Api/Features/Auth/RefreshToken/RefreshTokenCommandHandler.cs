using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using BCrypt.Net;

namespace EduConnect.Api.Features.Auth.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        AppDbContext context,
        JwtTokenService jwtTokenService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new UnauthorizedException("HTTP context is not available.");
        }

        if (!context.Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
        {
            throw new UnauthorizedException("Refresh token is missing.");
        }

        // Reuse detection: we look up by ID (and verify the secret hash)
        // WITHOUT filtering on IsRevoked, so a replay of an already-rotated
        // token is observable. Handled below.
        Infrastructure.Database.Entities.RefreshTokenEntity? storedToken = null;

        if (_jwtTokenService.TryParseRefreshToken(refreshToken, out var refreshTokenId, out var refreshTokenSecret))
        {
            storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Id == refreshTokenId, cancellationToken);

            if (storedToken != null && !BCrypt.Net.BCrypt.EnhancedVerify(refreshTokenSecret, storedToken.TokenHash))
            {
                // Token ID matched but the secret didn't — treat as unknown.
                storedToken = null;
            }
        }
        else
        {
            // Legacy format without the id.secret split. Fall back to linear
            // scan on active tokens only (reuse detection isn't possible in
            // this branch because no id is presented).
            var activeTokens = await _context.RefreshTokens
                .Include(rt => rt.User)
                .Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken);

            storedToken = activeTokens
                .FirstOrDefault(rt => BCrypt.Net.BCrypt.EnhancedVerify(refreshToken, rt.TokenHash));
        }

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh attempted with unknown token");
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Replay of an already-rotated or manually-revoked token: assume the
        // cookie has been stolen and burn down the whole family so the
        // attacker's session and any concurrent legitimate sessions must
        // re-authenticate through login.
        if (storedToken.IsRevoked)
        {
            var now = DateTimeOffset.UtcNow;
            var familyTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId && !rt.IsRevoked && rt.ExpiresAt > now)
                .ToListAsync(cancellationToken);

            foreach (var rt in familyTokens)
            {
                rt.IsRevoked = true;
                rt.RevokedAt = now;
            }
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoked {FamilySize} active token(s)",
                storedToken.UserId,
                familyTokens.Count);

            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        if (storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Refresh attempted with expired token {TokenId}", storedToken.Id);
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var user = storedToken.User;
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("User {UserId} is not active", storedToken.UserId);
            throw new UnauthorizedException("User is inactive.");
        }

        // Same legacy-rotation check as login: mirror the effective flag so a
        // rotated access token still carries must_change_password when the
        // user's password pre-dates the policy cutoff.
        var mustChange = user.MustChangePassword
            || PasswordPolicy.IsLegacyPassword(user.PasswordUpdatedAt);

        var newAccessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.SchoolId,
            user.Role,
            user.Name,
            JwtTokenService.AccessTokenLifetimeMinutes,
            user.MustChangePassword);
        var newRefreshTokenId = Guid.NewGuid();
        var newRefreshTokenSecret = _jwtTokenService.GenerateRefreshToken();
        var newRefreshToken = _jwtTokenService.BuildRefreshToken(newRefreshTokenId, newRefreshTokenSecret);
        var newRefreshTokenHash = BCrypt.Net.BCrypt.EnhancedHashPassword(newRefreshTokenSecret, 12);

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTimeOffset.UtcNow;

        var newRefreshTokenEntity = new Infrastructure.Database.Entities.RefreshTokenEntity
        {
            Id = newRefreshTokenId,
            UserId = user.Id,
            SchoolId = user.SchoolId,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            ReplacedById = storedToken.Id
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);

        return new RefreshTokenResponse(
            newAccessToken,
            JwtTokenService.AccessTokenLifetimeSeconds,
            newRefreshToken,
            user.MustChangePassword);
    }
}
