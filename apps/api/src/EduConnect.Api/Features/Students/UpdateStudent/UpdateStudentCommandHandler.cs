using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Students.UpdateStudent;

public class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand, UpdateStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpdateStudentCommandHandler> _logger;

    public UpdateStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpdateStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpdateStudentResponse> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can update students.");
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s =>
                s.Id == request.Id &&
                s.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (student == null)
        {
            throw new NotFoundException($"Student with ID {request.Id} not found.");
        }

        // If class is changing, verify new class exists in this school
        if (student.ClassId != request.ClassId)
        {
            var classExists = await _context.Classes
                .AnyAsync(c =>
                    c.Id == request.ClassId &&
                    c.SchoolId == _currentUserService.SchoolId,
                    cancellationToken);

            if (!classExists)
            {
                throw new NotFoundException($"Class with ID {request.ClassId} not found.");
            }

            // Verify roll number doesn't conflict in the new class
            var rollNumberConflict = await _context.Students
                .AnyAsync(s =>
                    s.SchoolId == _currentUserService.SchoolId &&
                    s.ClassId == request.ClassId &&
                    s.RollNumber == student.RollNumber &&
                    s.Id != student.Id,
                    cancellationToken);

            if (rollNumberConflict)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "ClassId", new[] { $"Roll number '{student.RollNumber}' already exists in the target class." } }
                });
            }
        }

        student.Name = request.Name.Trim();
        student.ClassId = request.ClassId;
        student.DateOfBirth = request.DateOfBirth;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Student updated: {StudentId} by admin {AdminId}",
            student.Id, _currentUserService.UserId);

        return new UpdateStudentResponse(student.Id, "Student updated successfully.");
    }
}
