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
        // Look up the active student by roll number.
        // NOTE: this is an anonymous endpoint so EF Core global query filters
        // do not scope by school_id. We deliberately accept any school's
        // roll number here — the PIN then acts as the second factor that proves
        // the caller is the linked parent. Roll numbers are school-unique by
        // the DB unique index (school_id, class_id, roll_number), so collisions
        // across schools are only possible if two schools share identical codes.
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.RollNumber == request.RollNumber && s.IsActive, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Parent login attempt with invalid or inactive roll number {RollNumber}", request.RollNumber);
            throw new UnauthorizedException("Invalid roll number or PIN.");
        }

        // Load ALL active parents linked to this student so we can test each PIN.
        // A student can have multiple parents; we must find the one whose PIN matches
        // rather than picking arbitrarily with FirstOrDefault.
        var parentLinks = await _context.ParentStudentLinks
            .Include(l => l.Parent)
            .Where(
                l => l.StudentId == student.Id &&
                     l.Parent != null &&
                     l.Parent!.Role == "Parent" &&
                     l.Parent!.IsActive)
            .ToListAsync(cancellationToken);

        if (parentLinks.Count == 0)
        {
            _logger.LogWarning("No active parent found for student {StudentId} with roll number {RollNumber}", student.Id, request.RollNumber);
            throw new UnauthorizedException("Invalid roll number or PIN.");
        }

        // Find the parent whose PIN matches the supplied value.
        var user = parentLinks
            .Select(l => l.Parent!)
            .FirstOrDefault(p => !string.IsNullOrEmpty(p.PinHash) && _pinService.VerifyPin(request.Pin, p.PinHash));

        if (user == null)
        {
            _logger.LogWarning("Invalid PIN attempt for roll number {RollNumber} (student {StudentId})", request.RollNumber, student.Id);
            // Generic message — do not reveal whether roll number exists or PIN is unset.
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
