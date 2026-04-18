using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Email;
using EduConnect.Api.Infrastructure.Services;

namespace EduConnect.Api.Features.Parents.CreateParent;

public class CreateParentCommandHandler : IRequestHandler<CreateParentCommand, CreateParentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PinService _pinService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateParentCommandHandler> _logger;

    public CreateParentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PinService pinService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<CreateParentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CreateParentResponse> Handle(CreateParentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can create parent accounts.");
        }

        var trimmedName = request.Name.Trim();
        var normalizedPhone = JapanPhoneNumber.NormalizeUserInput(request.Phone);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var phoneExists = await _context.Users
            .AnyAsync(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Phone == normalizedPhone,
                cancellationToken);

        if (phoneExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Phone", new[] { "A user with this phone number already exists." } }
            });
        }

        var emailExists = await _context.Users
            .AnyAsync(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Email != null &&
                u.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (emailExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Email", new[] { "A user with this email already exists." } }
            });
        }

        var parent = new UserEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Name = trimmedName,
            Phone = normalizedPhone,
            Email = normalizedEmail,
            Role = "Parent",
            PinHash = _pinService.HashPin(request.Pin),
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Users.Add(parent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Parent {ParentId} created by admin {AdminId}",
            parent.Id,
            _currentUserService.UserId);

        // Fire-and-log: welcome email failure must not fail account creation.
        try
        {
            var school = await _context.Schools
                .FirstOrDefaultAsync(s => s.Id == _currentUserService.SchoolId, cancellationToken);

            if (school is not null && !string.IsNullOrWhiteSpace(parent.Email))
            {
                var appUrl = EmailBranding.ResolveAppUrl(_configuration);
                var logoUrl = EmailBranding.ResolveLogoUrl(_configuration);
                var loginUrl = $"{appUrl}/login";

                var content = EmailTemplates.BuildWelcomeParent(
                    school,
                    parentName: parent.Name,
                    studentName: "your child",
                    loginUrl: loginUrl,
                    tempPin: request.Pin,
                    logoUrl: logoUrl);

                await _emailService.SendEmailAsync(
                    parent.Email!,
                    content.Subject,
                    content.Html,
                    content.Text,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to dispatch welcome email for Parent {ParentId}",
                parent.Id);
        }

        return new CreateParentResponse(
            parent.Id,
            "Parent created successfully. Share the temporary PIN with them — they will be required to change it on first login.",
            request.Pin);
    }
}
