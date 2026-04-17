using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Logging;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Auth.LoginParent;

public class LoginParentCommandHandler : IRequestHandler<LoginParentCommand, LoginParentResponse>
{
    private readonly AppDbContext _context;
    private readonly PinService _pinService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LoginParentCommandHandler> _logger;

    public LoginParentCommandHandler(
        AppDbContext context,
        PinService pinService,
        JwtTokenService jwtTokenService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LoginParentCommandHandler> logger)
    {
        _context = context;
        _pinService = pinService;
        _jwtTokenService = jwtTokenService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<LoginParentResponse> Handle(LoginParentCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = JapanPhoneNumber.NormalizeUserInput(request.Phone);

        // NOTE: this is an anonymous endpoint so EF Core global query filters
        // do not scope by school_id. Phone is school-unique in the DB, not
        // globally unique, so we verify the PIN against every active parent
        // account matching the normalized phone and then select the first match.
        var matchingPhoneParents = await _context.Users
            .Where(u =>
                u.Phone == normalizedPhone &&
                u.Role == "Parent" &&
                u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        if (matchingPhoneParents.Count == 0)
        {
            _logger.LogWarning(
                "Parent login attempt for non-existent or inactive parent user (phoneMasked={PhoneMasked})",
                LogRedaction.MaskPhone(request.Phone));
            throw new UnauthorizedException("Invalid phone or PIN.");
        }

        var user = matchingPhoneParents
            .FirstOrDefault(p => !string.IsNullOrEmpty(p.PinHash) && _pinService.VerifyPin(request.Pin, p.PinHash));

        if (user == null)
        {
            _logger.LogWarning(
                "Invalid parent PIN attempt (phoneMasked={PhoneMasked})",
                LogRedaction.MaskPhone(request.Phone));
            throw new UnauthorizedException("Invalid phone or PIN.");
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

        _logger.LogInformation("Parent user {UserId} logged in successfully", user.Id);

        return new LoginParentResponse(accessToken);
    }
}
