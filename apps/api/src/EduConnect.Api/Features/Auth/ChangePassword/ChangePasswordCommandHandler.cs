using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Auth.ChangePassword;

/// <summary>
/// Staff (Teacher/Admin) change their password while authenticated. Verifies
/// the current password, replaces the hash, clears <c>must_change_password</c>,
/// and revokes all active refresh tokens to force re-login across devices.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PasswordHasher passwordHasher,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new UnauthorizedException("Authentication required.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("Account is not available.");
        }

        if (user.Role == "Parent")
        {
            throw new ForbiddenException("Parents must use the change-pin endpoint.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning(
                "Change-password failed: current password is incorrect for user {UserId}",
                user.Id);
            throw new UnauthorizedException("Current password is incorrect.");
        }

        var now = DateTimeOffset.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.PasswordUpdatedAt = now;
        user.UpdatedAt = now;

        // Revoke all active refresh tokens for this user (force re-login everywhere).
        var activeRefreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var rt in activeRefreshTokens)
        {
            rt.IsRevoked = true;
            rt.RevokedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {UserId}", user.Id);

        return Unit.Value;
    }
}
