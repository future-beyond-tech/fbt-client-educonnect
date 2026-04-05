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

        Infrastructure.Database.Entities.RefreshTokenEntity? storedToken = null;

        if (_jwtTokenService.TryParseRefreshToken(refreshToken, out var refreshTokenId, out var refreshTokenSecret))
        {
            storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt =>
                    rt.Id == refreshTokenId &&
                    !rt.IsRevoked &&
                    rt.ExpiresAt > DateTimeOffset.UtcNow,
                    cancellationToken);

            if (storedToken != null && !BCrypt.Net.BCrypt.EnhancedVerify(refreshTokenSecret, storedToken.TokenHash))
            {
                storedToken = null;
            }
        }
        else
        {
            var activeTokens = await _context.RefreshTokens
                .Include(rt => rt.User)
                .Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken);

            storedToken = activeTokens
                .FirstOrDefault(rt => BCrypt.Net.BCrypt.EnhancedVerify(refreshToken, rt.TokenHash));
        }

        if (storedToken == null)
        {
            _logger.LogWarning("No matching active refresh token found in database");
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var user = storedToken.User;
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("User {UserId} is not active", storedToken.UserId);
            throw new UnauthorizedException("User is inactive.");
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.SchoolId, user.Role, user.Name, 15);
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
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            ReplacedById = storedToken.Id
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);

        return new RefreshTokenResponse(newAccessToken, newRefreshToken);
    }
}
