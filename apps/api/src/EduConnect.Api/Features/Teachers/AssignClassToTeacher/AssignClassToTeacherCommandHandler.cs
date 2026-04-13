using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Teachers.AssignClassToTeacher;

public class AssignClassToTeacherCommandHandler : IRequestHandler<AssignClassToTeacherCommand, AssignClassToTeacherResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<AssignClassToTeacherCommandHandler> _logger;

    public AssignClassToTeacherCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<AssignClassToTeacherCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<AssignClassToTeacherResponse> Handle(AssignClassToTeacherCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can assign classes to teachers.");
        }

        // Verify teacher exists and has Teacher role
        var teacher = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == request.TeacherId &&
                u.SchoolId == _currentUserService.SchoolId &&
                u.Role == "Teacher",
                cancellationToken);

        if (teacher == null)
        {
            throw new NotFoundException($"Teacher with ID {request.TeacherId} not found.");
        }

        // Verify class exists
        var classExists = await _context.Classes
            .AnyAsync(c =>
                c.Id == request.ClassId &&
                c.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (!classExists)
        {
            throw new NotFoundException($"Class with ID {request.ClassId} not found.");
        }

        // Verify subject exists in the subjects reference table
        var subjectExists = await _context.Subjects
            .AnyAsync(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.Name == request.Subject,
                cancellationToken);

        if (!subjectExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Subject", new[] { $"Subject '{request.Subject}' does not exist. Create it first." } }
            });
        }

        // Check for duplicate assignment
        var duplicateExists = await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.TeacherId == request.TeacherId &&
                tca.ClassId == request.ClassId &&
                tca.Subject == request.Subject,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "ClassId", new[] { "This teacher is already assigned to this class and subject." } }
            });
        }

        var assignment = new TeacherClassAssignmentEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            TeacherId = request.TeacherId,
            ClassId = request.ClassId,
            Subject = request.Subject,
            IsClassTeacher = request.IsClassTeacher,
            AssignedById = _currentUserService.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (request.IsClassTeacher)
        {
            var existingClassTeacher = await _context.TeacherClassAssignments
                .FirstOrDefaultAsync(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.ClassId == request.ClassId &&
                    tca.IsClassTeacher,
                    cancellationToken);

            if (existingClassTeacher != null)
            {
                existingClassTeacher.IsClassTeacher = false;
            }
        }

        _context.TeacherClassAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Teacher {TeacherId} assigned to class {ClassId} for subject '{Subject}' by admin {AdminId}. ClassTeacher={IsClassTeacher}",
            request.TeacherId, request.ClassId, request.Subject, _currentUserService.UserId, request.IsClassTeacher);

        return new AssignClassToTeacherResponse(assignment.Id, "Teacher assigned successfully.");
    }
}
