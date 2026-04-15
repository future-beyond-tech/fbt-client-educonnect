using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Logging;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using BCrypt.Net;

namespace EduConnect.Api.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, string>
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        AppDbContext context,
        PasswordHasher passwordHasher,
        JwtTokenService jwtTokenService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LoginCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<string> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == request.Phone && u.IsActive, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning(
                "Login attempt for non-existent or inactive staff user (phoneMasked={PhoneMasked})",
                LogRedaction.MaskPhone(request.Phone));
            throw new UnauthorizedException("Invalid phone or password.");
        }

        if (user.Role == "Parent")
        {
            _logger.LogWarning(
                "Parent user attempted staff login (phoneMasked={PhoneMasked})",
                LogRedaction.MaskPhone(request.Phone));
            throw new UnauthorizedException("Parents must use PIN login.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "Invalid password attempt for staff user {UserId} (phoneMasked={PhoneMasked})",
                user.Id,
                LogRedaction.MaskPhone(request.Phone));
            throw new UnauthorizedException("Invalid phone or password.");
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.SchoolId, user.Role, user.Name, 15);
        var refreshTokenId = Guid.NewGuid();
        var refreshTokenSecret = _jwtTokenService.GenerateRefreshToken();
        var refreshToken = _jwtTokenService.BuildRefreshToken(refreshTokenId, refreshTokenSecret);
        var refreshTokenHash = BCrypt.Net.BCrypt.EnhancedHashPassword(refreshTokenSecret, 12);
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var refreshTokenEntity = new Infrastructure.Database.Entities.RefreshTokenEntity
        {
            Id = refreshTokenId,
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = refreshTokenExpiresAt,
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Response.Cookies.Append(
                "refresh_token",
                refreshToken,
                RefreshTokenCookieOptions.Create(httpContext.Request, refreshTokenExpiresAt));
        }

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return accessToken;
    }
}
