using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Students.GetStudentById;

public class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentDetailDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<GetStudentByIdQueryHandler> _logger;

    public GetStudentByIdQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<GetStudentByIdQueryHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<StudentDetailDto> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .Include(s => s.Class)
            .Include(s => s.ParentLinks)
                .ThenInclude(pl => pl.Parent)
            .FirstOrDefaultAsync(s =>
                s.Id == request.Id &&
                s.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (student == null)
        {
            throw new NotFoundException($"Student with ID {request.Id} not found.");
        }

        // TEACHER: Can only view students in their assigned classes
        if (_currentUserService.Role == "Teacher")
        {
            var isAssigned = await _context.TeacherClassAssignments
                .AnyAsync(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId &&
                    tca.ClassId == student.ClassId,
                    cancellationToken);

            if (!isAssigned)
            {
                throw new ForbiddenException("You can only view students in your assigned classes.");
            }

            // Teachers cannot see deactivated students
            if (!student.IsActive)
            {
                throw new NotFoundException($"Student with ID {request.Id} not found.");
            }
        }
        // PARENT: Can only view their own linked students
        else if (_currentUserService.Role == "Parent")
        {
            var isLinked = await _context.ParentStudentLinks
                .AnyAsync(psl =>
                    psl.SchoolId == _currentUserService.SchoolId &&
                    psl.ParentId == _currentUserService.UserId &&
                    psl.StudentId == request.Id,
                    cancellationToken);

            if (!isLinked)
            {
                throw new ForbiddenException("You can only view your own linked students.");
            }

            // Parents cannot see deactivated students
            if (!student.IsActive)
            {
                throw new NotFoundException($"Student with ID {request.Id} not found.");
            }
        }
        // ADMIN: Can view any student (including inactive)

        var parentLinks = student.ParentLinks
            .Select(pl => new ParentLinkDto(
                pl.Id,
                pl.ParentId,
                pl.Parent?.Name ?? string.Empty,
                pl.Parent?.Phone ?? string.Empty,
                pl.Parent?.Email ?? string.Empty,
                pl.Relationship,
                pl.CreatedAt))
            .OrderBy(pl => pl.ParentName)
            .ToList();

        return new StudentDetailDto(
            student.Id,
            student.Name,
            student.RollNumber,
            student.ClassId,
            student.Class?.Name ?? string.Empty,
            student.Class?.Section ?? string.Empty,
            student.Class?.AcademicYear ?? string.Empty,
            student.DateOfBirth,
            student.IsActive,
            student.CreatedAt,
            parentLinks);
    }
}
