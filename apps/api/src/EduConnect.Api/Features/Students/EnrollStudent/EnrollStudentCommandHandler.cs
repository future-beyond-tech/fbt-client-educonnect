using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Email;
using EduConnect.Api.Infrastructure.Services;

namespace EduConnect.Api.Features.Students.EnrollStudent;

public class EnrollStudentCommandHandler : IRequestHandler<EnrollStudentCommand, EnrollStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PinService _pinService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnrollStudentCommandHandler> _logger;

    public EnrollStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PinService pinService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<EnrollStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EnrollStudentResponse> Handle(EnrollStudentCommand request, CancellationToken cancellationToken)
    {
        // Admins can enroll into any class in their school.
        // Class teachers (IsClassTeacher = true) can enroll into their own
        // class(es) only — this is the guard that delivers the "no access to
        // other classes" requirement for the class-teacher self-service flow.
        // Any other role is forbidden outright.
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to enroll a student",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only admins and class teachers can enroll students.");
        }

        if (_currentUserService.Role == "Teacher")
        {
            var isClassTeacherOfTarget = await _context.TeacherClassAssignments
                .AnyAsync(tca =>
                    tca.TeacherId == _currentUserService.UserId &&
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.ClassId == request.ClassId &&
                    tca.IsClassTeacher,
                    cancellationToken);

            if (!isClassTeacherOfTarget)
            {
                _logger.LogWarning(
                    "Teacher {UserId} attempted to enroll a student into class {ClassId} but is not its class teacher",
                    _currentUserService.UserId, request.ClassId);
                throw new ForbiddenException("You can only enroll students into a class where you are the class teacher.");
            }
        }

        if (request.Parent is not null && request.ExistingParent is not null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Parent", ["Choose either a new parent or an existing parent, not both."] }
            });
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
        UserEntity? existingParent = null;
        ParentStudentLinkEntity? parentLink = null;

        if (request.Parent is not null)
        {
            var normalizedPhone = JapanPhoneNumber.NormalizeUserInput(request.Parent.Phone);
            var normalizedEmail = request.Parent.Email.Trim().ToLowerInvariant();

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
        }
        else if (request.ExistingParent is not null)
        {
            existingParent = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == request.ExistingParent.ParentId &&
                    u.SchoolId == _currentUserService.SchoolId &&
                    u.Role == "Parent" &&
                    u.IsActive,
                    cancellationToken);

            if (existingParent == null)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "ExistingParent.ParentId", ["Select an active parent account from this school."] }
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
                Phone = JapanPhoneNumber.NormalizeUserInput(request.Parent.Phone),
                Email = request.Parent.Email.Trim().ToLowerInvariant(),
                Role = "Parent",
                PinHash = _pinService.HashPin(request.Parent.Pin),
                IsActive = true,
                MustChangePassword = true,
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
        else if (request.ExistingParent is not null && existingParent is not null)
        {
            parentLink = new ParentStudentLinkEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                ParentId = existingParent.Id,
                StudentId = student.Id,
                Relationship = request.ExistingParent.Relationship.Trim().ToLowerInvariant(),
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
            "Student enrolled: {StudentId} in class {ClassId} by {Role} {ActorId}. ParentCreated={ParentCreated} LinkedParentId={ParentId}",
            student.Id, request.ClassId, _currentUserService.Role, _currentUserService.UserId, parent is not null, parent?.Id ?? existingParent?.Id);

        // Fire-and-log: only send welcome email when a new parent was created
        // inline as part of enrollment. Linking an existing parent reuses
        // their existing credentials, so no welcome email is needed.
        if (parent is not null && request.Parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
        {
            try
            {
                var school = await _context.Schools
                    .FirstOrDefaultAsync(s => s.Id == _currentUserService.SchoolId, cancellationToken);

                if (school is not null)
                {
                    var appUrl = EmailBranding.ResolveAppUrl(_configuration);
                    var logoUrl = EmailBranding.ResolveLogoUrl(_configuration);
                    var loginUrl = $"{appUrl}/login";

                    var content = EmailTemplates.BuildWelcomeParent(
                        school,
                        parentName: parent.Name,
                        studentName: student.Name,
                        loginUrl: loginUrl,
                        tempPin: request.Parent.Pin,
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
                    "Failed to dispatch welcome email for new Parent {ParentId} (enrolled with Student {StudentId})",
                    parent.Id,
                    student.Id);
            }
        }

        return new EnrollStudentResponse(
            student.Id,
            "Student enrolled successfully.",
            request.Parent?.Pin);
    }
}
