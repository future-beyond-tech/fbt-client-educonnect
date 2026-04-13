using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Teachers.CreateTeacher;

public class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, CreateTeacherResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PasswordHasher _passwordHasher;
    private readonly ILogger<CreateTeacherCommandHandler> _logger;

    public CreateTeacherCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PasswordHasher passwordHasher,
        ILogger<CreateTeacherCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<CreateTeacherResponse> Handle(CreateTeacherCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can create teachers.");
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

        var teacher = new UserEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Name = trimmedName,
            Phone = trimmedPhone,
            Email = normalizedEmail,
            Role = "Teacher",
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Users.Add(teacher);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Teacher {TeacherId} created by admin {AdminId}",
            teacher.Id,
            _currentUserService.UserId);

        return new CreateTeacherResponse(teacher.Id, "Teacher created successfully.");
    }
}
