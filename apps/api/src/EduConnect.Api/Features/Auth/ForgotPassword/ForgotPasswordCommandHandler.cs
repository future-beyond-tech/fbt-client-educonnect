using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Auth.ForgotPassword;

/// <summary>
/// Handles forgot-password requests for staff users (Teacher / Admin).
/// Always succeeds from the caller's perspective to avoid leaking which
/// emails are registered. If a matching active staff user exists, a reset
/// token is generated, hashed and persisted, and a reset link is emailed
/// via Resend.
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(60);

    private readonly AppDbContext _context;
    private readonly ResetTokenService _resetTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        AppDbContext context,
        ResetTokenService resetTokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _context = context;
        _resetTokenService = resetTokenService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Email != null &&
                     u.Email.ToLower() == normalizedEmail &&
                     u.IsActive &&
                     u.Role != "Parent",
                cancellationToken);

        if (user == null)
        {
            _logger.LogInformation("Forgot-password requested for unknown or non-staff email");
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
            Purpose = AuthResetTokenPurpose.Password,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.AuthResetTokens.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var appUrl = (_configuration["NEXT_PUBLIC_APP_URL"] ?? _configuration["APP_PUBLIC_URL"] ?? "http://localhost:3000")
            .TrimEnd('/');
        var resetLink = $"{appUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        var subject = "Reset your EduConnect password";
        var html = $"""
                    <p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)},</p>
                    <p>We received a request to reset your EduConnect password. Click the link below to choose a new one. This link will expire in 60 minutes and can only be used once.</p>
                    <p><a href="{resetLink}">Reset my password</a></p>
                    <p>If you didn't request this, you can safely ignore this email.</p>
                    <p>— The EduConnect team</p>
                    """;
        var text = $"Hi {user.Name},\n\nReset your EduConnect password using the link below (valid for 60 minutes):\n{resetLink}\n\nIf you didn't request this, you can ignore this email.";

        await _emailService.SendEmailAsync(user.Email!, subject, html, text, cancellationToken);

        _logger.LogInformation("Issued password reset token {TokenId} for staff user {UserId}", entity.Id, user.Id);

        return Unit.Value;
    }
}
