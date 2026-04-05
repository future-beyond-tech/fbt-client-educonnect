using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using BCrypt.Net;

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
        // Look up the student by roll number
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.RollNumber == request.RollNumber, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Parent login attempt with invalid roll number {RollNumber}", request.RollNumber);
            throw new UnauthorizedException("Invalid roll number or PIN.");
        }

        // Find the parent linked to this student
        var parentLink = await _context.ParentStudentLinks
            .Include(l => l.Parent)
            .FirstOrDefaultAsync(
                l => l.StudentId == student.Id &&
                     l.Parent != null &&
                     l.Parent!.Role == "Parent" &&
                     l.Parent!.IsActive,
                cancellationToken);

        if (parentLink == null)
        {
            _logger.LogWarning("No active parent found for student {StudentId} with roll number {RollNumber}", student.Id, request.RollNumber);
            throw new UnauthorizedException("Invalid roll number or PIN.");
        }

        var user = parentLink.Parent;
        if (user == null)
        {
            _logger.LogWarning("Parent link {ParentLinkId} has no parent user loaded", parentLink.Id);
            throw new UnauthorizedException("Invalid roll number or PIN.");
        }

        var pinHash = user.PinHash;
        if (string.IsNullOrEmpty(pinHash))
        {
            _logger.LogWarning("Parent login attempt for user {UserId} with no PIN set", user.Id);
            throw new UnauthorizedException("PIN not set. Please contact school admin.");
        }

        if (!_pinService.VerifyPin(request.Pin, pinHash))
        {
            _logger.LogWarning("Invalid PIN attempt for parent user {UserId}", user.Id);
            throw new UnauthorizedException("Invalid roll number or PIN.");
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
