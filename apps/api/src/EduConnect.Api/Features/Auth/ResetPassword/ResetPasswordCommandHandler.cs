using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Auth.ResetPassword;

/// <summary>
/// Consumes a single-use password-reset token. The token is hashed and
/// looked up in <c>auth_reset_tokens</c>; if a valid, unused, non-expired
/// row exists for a Password reset, the user's password hash is replaced
/// and all of the user's refresh tokens are revoked. The reset token is
/// marked as used and cannot be reused.
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly ResetTokenService _resetTokenService;
    private readonly PasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        AppDbContext context,
        ResetTokenService resetTokenService,
        PasswordHasher passwordHasher,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _context = context;
        _resetTokenService = resetTokenService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _resetTokenService.HashToken(request.Token);

        var resetToken = await _context.AuthResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.Purpose == AuthResetTokenPurpose.Password,
                cancellationToken);

        if (resetToken == null)
        {
            _logger.LogWarning("Password reset attempted with unknown token");
            throw new UnauthorizedException("Reset link is invalid or has expired.");
        }

        if (resetToken.UsedAt != null)
        {
            _logger.LogWarning("Password reset attempted with already-used token {TokenId}", resetToken.Id);
            throw new UnauthorizedException("Reset link has already been used.");
        }

        if (resetToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Password reset attempted with expired token {TokenId}", resetToken.Id);
            throw new UnauthorizedException("Reset link is invalid or has expired.");
        }

        var user = resetToken.User;
        if (user == null || !user.IsActive || user.Role == "Parent")
        {
            _logger.LogWarning("Password reset token {TokenId} resolves to no usable staff user", resetToken.Id);
            throw new UnauthorizedException("Reset link is invalid or has expired.");
        }

        var now = DateTimeOffset.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.PasswordUpdatedAt = now;
        user.UpdatedAt = now;

        resetToken.UsedAt = now;

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

        _logger.LogInformation("Password reset successfully for user {UserId}", user.Id);

        return Unit.Value;
    }
}
