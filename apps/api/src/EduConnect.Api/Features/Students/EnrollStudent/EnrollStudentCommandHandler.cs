using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Students.EnrollStudent;

public class EnrollStudentCommandHandler : IRequestHandler<EnrollStudentCommand, EnrollStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<EnrollStudentCommandHandler> _logger;

    public EnrollStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<EnrollStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
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
                s.RollNumber == request.RollNumber,
                cancellationToken);

        if (rollNumberExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "RollNumber", new[] { $"Roll number '{request.RollNumber}' already exists in this class." } }
            });
        }

        var student = new StudentEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            ClassId = request.ClassId,
            Name = request.Name.Trim(),
            RollNumber = request.RollNumber.Trim(),
            DateOfBirth = request.DateOfBirth,
            IsActive = true,
            CreatedById = _currentUserService.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Student enrolled: {StudentId} in class {ClassId} by admin {AdminId}",
            student.Id, request.ClassId, _currentUserService.UserId);

        return new EnrollStudentResponse(student.Id, "Student enrolled successfully.");
    }
}
