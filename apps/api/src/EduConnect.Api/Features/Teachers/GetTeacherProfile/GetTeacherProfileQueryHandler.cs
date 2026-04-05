using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Teachers.GetTeacherProfile;

public class GetTeacherProfileQueryHandler : IRequestHandler<GetTeacherProfileQuery, TeacherProfileDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetTeacherProfileQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<TeacherProfileDto> Handle(GetTeacherProfileQuery request, CancellationToken cancellationToken)
    {
        // Teachers can view their own profile; admins can view any teacher
        if (_currentUserService.Role == "Teacher" && _currentUserService.UserId != request.TeacherId)
        {
            throw new ForbiddenException("You can only view your own profile.");
        }

        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and teachers can view teacher profiles.");
        }

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

        var assignments = await _context.TeacherClassAssignments
            .Where(tca =>
                tca.TeacherId == request.TeacherId &&
                tca.SchoolId == _currentUserService.SchoolId)
            .Include(tca => tca.Class)
            .OrderBy(tca => tca.Class!.Name)
            .ThenBy(tca => tca.Class!.Section)
            .ThenBy(tca => tca.Subject)
            .Select(tca => new TeacherAssignmentDto(
                tca.Id,
                tca.ClassId,
                tca.Class != null ? tca.Class.Name : string.Empty,
                tca.Class != null ? tca.Class.Section : string.Empty,
                tca.Subject,
                tca.CreatedAt))
            .ToListAsync(cancellationToken);

        return new TeacherProfileDto(
            teacher.Id,
            teacher.Name,
            teacher.Phone,
            teacher.IsActive,
            teacher.CreatedAt,
            assignments);
    }
}
