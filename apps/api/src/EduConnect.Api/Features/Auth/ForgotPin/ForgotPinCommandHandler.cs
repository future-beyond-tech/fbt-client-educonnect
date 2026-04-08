using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Auth.ForgotPin;

/// <summary>
/// Handles forgot-PIN requests for parents. Always returns success to
/// avoid account enumeration. If a matching active parent user with that
/// email exists, generates a single-use reset token, persists its SHA-256
/// hash, and emails the parent a link to <c>/reset-pin?token=...</c> via
/// Resend.
/// </summary>
public class ForgotPinCommandHandler : IRequestHandler<ForgotPinCommand, Unit>
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(60);

    private readonly AppDbContext _context;
    private readonly ResetTokenService _resetTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForgotPinCommandHandler> _logger;

    public ForgotPinCommandHandler(
        AppDbContext context,
        ResetTokenService resetTokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ForgotPinCommandHandler> logger)
    {
        _context = context;
        _resetTokenService = resetTokenService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Unit> Handle(ForgotPinCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email != null &&
                     u.Email.ToLower() == normalizedEmail &&
                     u.IsActive &&
                     u.Role == "Parent",
                cancellationToken);

        if (user == null)
        {
            _logger.LogInformation("Forgot-PIN requested for unknown or non-parent email");
            return Unit.Value;
        }

        var rawToken = _resetTokenService.GenerateRawToken();
        var tokenHash = _resetTokenService.HashToken(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(ResetTokenLifetime);

        var entity = new AuthResetTokenEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = user.SchoolId,
            UserId = user.Id,
            TokenHash = tokenHash,
            Purpose = AuthResetTokenPurpose.Pin,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.AuthResetTokens.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var appUrl = (_configuration["NEXT_PUBLIC_APP_URL"] ?? _configuration["APP_PUBLIC_URL"] ?? "http://localhost:3000")
            .TrimEnd('/');
        var resetLink = $"{appUrl}/reset-pin?token={Uri.EscapeDataString(rawToken)}";

        var subject = "Reset your EduConnect PIN";
        var html = $"""
                    <p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)},</p>
                    <p>We received a request to reset your EduConnect parent PIN. Click the link below to choose a new PIN. The link will expire in 60 minutes and can only be used once.</p>
                    <p><a href="{resetLink}">Reset my PIN</a></p>
                    <p>If you didn't request this, you can safely ignore this email — your PIN will stay the same.</p>
                    <p>— The EduConnect team</p>
                    """;
        var text = $"Hi {user.Name},\n\nReset your EduConnect parent PIN using the link below (valid for 60 minutes):\n{resetLink}\n\nIf you didn't request this, you can ignore this email.";

        await _emailService.SendEmailAsync(user.Email!, subject, html, text, cancellationToken);

        _logger.LogInformation("Issued PIN reset token {TokenId} for parent user {UserId}", entity.Id, user.Id);

        return Unit.Value;
    }
}
