using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Auth.ResetPin;

/// <summary>
/// Consumes a single-use parent PIN-reset token. Verifies that the token
/// hash exists, is unused, has not expired, and belongs to an active
/// parent user. On success, replaces the user's PIN hash, marks the token
/// used, and revokes all of the parent's active refresh tokens.
/// </summary>
public class ResetPinCommandHandler : IRequestHandler<ResetPinCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly ResetTokenService _resetTokenService;
    private readonly PinService _pinService;
    private readonly ILogger<ResetPinCommandHandler> _logger;

    public ResetPinCommandHandler(
        AppDbContext context,
        ResetTokenService resetTokenService,
        PinService pinService,
        ILogger<ResetPinCommandHandler> logger)
    {
        _context = context;
        _resetTokenService = resetTokenService;
        _pinService = pinService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResetPinCommand request, CancellationToken cancellationToken)
    {
        if (!_pinService.ValidatePinFormat(request.NewPin))
        {
            throw new Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    { "newPin", new[] { "PIN must be 4-6 digits." } }
                });
        }

        var tokenHash = _resetTokenService.HashToken(request.Token);

        var resetToken = await _context.AuthResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.Purpose == AuthResetTokenPurpose.Pin,
                cancellationToken);

        if (resetToken == null)
        {
            _logger.LogWarning("PIN reset attempted with unknown token");
            throw new UnauthorizedException("Reset link is invalid or has expired.");
        }

        if (resetToken.UsedAt != null)
        {
            _logger.LogWarning("PIN reset attempted with already-used token {TokenId}", resetToken.Id);
            throw new UnauthorizedException("Reset link has already been used.");
        }

        if (resetToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("PIN reset attempted with expired token {TokenId}", resetToken.Id);
            throw new UnauthorizedException("Reset link is invalid or has expired.");
        }

        var user = resetToken.User;
        if (user == null || !user.IsActive || user.Role != "Parent")
        {
            _logger.LogWarning("PIN reset token {TokenId} resolves to no usable parent user", resetToken.Id);
            throw new UnauthorizedException("Reset link is invalid or has expired.");
        }

        user.PinHash = _pinService.HashPin(request.NewPin);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        resetToken.UsedAt = DateTimeOffset.UtcNow;

        var activeRefreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var rt in activeRefreshTokens)
        {
            rt.IsRevoked = true;
            rt.RevokedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIN reset successfully for parent user {UserId}", user.Id);

        return Unit.Value;
    }
}
