using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Auth.ChangePin;

/// <summary>
/// Parents change their PIN while authenticated. Verifies the current PIN,
/// replaces the hash, clears <c>must_change_password</c> (the column is shared
/// for password and PIN), and revokes all active refresh tokens.
/// </summary>
public class ChangePinCommandHandler : IRequestHandler<ChangePinCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PinService _pinService;
    private readonly ILogger<ChangePinCommandHandler> _logger;

    public ChangePinCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PinService pinService,
        ILogger<ChangePinCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ChangePinCommand request, CancellationToken cancellationToken)
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

        if (user.Role != "Parent")
        {
            throw new ForbiddenException("Only parents can change their PIN.");
        }

        if (string.IsNullOrEmpty(user.PinHash) ||
            !_pinService.VerifyPin(request.CurrentPin, user.PinHash))
        {
            _logger.LogWarning(
                "Change-PIN failed: current PIN is incorrect for user {UserId}",
                user.Id);
            throw new UnauthorizedException("Current PIN is incorrect.");
        }

        user.PinHash = _pinService.HashPin(request.NewPin);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;

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

        _logger.LogInformation("PIN changed for user {UserId}", user.Id);

        return Unit.Value;
    }
}
