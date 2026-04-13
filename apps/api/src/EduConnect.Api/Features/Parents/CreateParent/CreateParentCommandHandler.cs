using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Parents.CreateParent;

public class CreateParentCommandHandler : IRequestHandler<CreateParentCommand, CreateParentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PinService _pinService;
    private readonly ILogger<CreateParentCommandHandler> _logger;

    public CreateParentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PinService pinService,
        ILogger<CreateParentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _logger = logger;
    }

    public async Task<CreateParentResponse> Handle(CreateParentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can create parent accounts.");
        }

        var trimmedName = request.Name.Trim();
        var trimmedPhone = request.Phone.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var phoneExists = await _context.Users
            .AnyAsync(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Phone == trimmedPhone,
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
            Phone = trimmedPhone,
            Email = normalizedEmail,
            Role = "Parent",
            PinHash = _pinService.HashPin(request.Pin),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Users.Add(parent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Parent {ParentId} created by admin {AdminId}",
            parent.Id,
            _currentUserService.UserId);

        return new CreateParentResponse(parent.Id, "Parent created successfully.");
    }
}
