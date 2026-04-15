using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Students.EnrollStudent;

public class EnrollStudentCommandHandler : IRequestHandler<EnrollStudentCommand, EnrollStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PinService _pinService;
    private readonly ILogger<EnrollStudentCommandHandler> _logger;

    public EnrollStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PinService pinService,
        ILogger<EnrollStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _logger = logger;
    }

    public async Task<EnrollStudentResponse> Handle(EnrollStudentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to enroll a student",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only admins can enroll students.");
        }

        var trimmedRollNumber = request.RollNumber.Trim();

        // Verify class exists in this school
        var classExists = await _context.Classes
            .AnyAsync(c =>
                c.Id == request.ClassId &&
                c.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (!classExists)
        {
            throw new NotFoundException($"Class with ID {request.ClassId} not found.");
        }

        // Validate roll number unique within school + class
        var rollNumberExists = await _context.Students
            .AnyAsync(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.ClassId == request.ClassId &&
                s.RollNumber == trimmedRollNumber,
                cancellationToken);

        if (rollNumberExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "RollNumber", new[] { $"Roll number '{trimmedRollNumber}' already exists in this class." } }
            });
        }

        UserEntity? parent = null;
        ParentStudentLinkEntity? parentLink = null;

        if (request.Parent is not null)
        {
            var trimmedPhone = request.Parent.Phone.Trim();
            var normalizedEmail = request.Parent.Email.Trim().ToLowerInvariant();

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
        }

        var student = new StudentEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            ClassId = request.ClassId,
            Name = request.Name.Trim(),
            RollNumber = trimmedRollNumber,
            DateOfBirth = request.DateOfBirth,
            IsActive = true,
            CreatedById = _currentUserService.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (request.Parent is not null)
        {
            parent = new UserEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                Name = request.Parent.Name.Trim(),
                Phone = request.Parent.Phone.Trim(),
                Email = request.Parent.Email.Trim().ToLowerInvariant(),
                Role = "Parent",
                PinHash = _pinService.HashPin(request.Parent.Pin),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            parentLink = new ParentStudentLinkEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                ParentId = parent.Id,
                StudentId = student.Id,
                Relationship = request.Parent.Relationship.Trim().ToLowerInvariant(),
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        _context.Students.Add(student);
        if (parent is not null)
        {
            _context.Users.Add(parent);
        }

        if (parentLink is not null)
        {
            _context.ParentStudentLinks.Add(parentLink);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Student enrolled: {StudentId} in class {ClassId} by admin {AdminId}. ParentCreated={ParentCreated} ParentId={ParentId}",
            student.Id, request.ClassId, _currentUserService.UserId, parent is not null, parent?.Id);

        return new EnrollStudentResponse(student.Id, "Student enrolled successfully.");
    }
}
