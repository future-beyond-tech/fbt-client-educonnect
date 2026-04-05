using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Teachers.GetClassesForTeacher;

public class GetClassesForTeacherQueryHandler : IRequestHandler<GetClassesForTeacherQuery, List<TeacherClassDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetClassesForTeacherQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<TeacherClassDto>> Handle(GetClassesForTeacherQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("This endpoint is for teachers only.");
        }

        var assignments = await _context.TeacherClassAssignments
            .Where(tca =>
                tca.TeacherId == _currentUserService.UserId &&
                tca.SchoolId == _currentUserService.SchoolId)
            .Include(tca => tca.Class)
            .OrderBy(tca => tca.Class!.Name)
            .ThenBy(tca => tca.Class!.Section)
            .ThenBy(tca => tca.Subject)
            .Select(tca => new TeacherClassDto(
                tca.ClassId,
                tca.Class != null ? tca.Class.Name : string.Empty,
                tca.Class != null ? tca.Class.Section : string.Empty,
                tca.Subject))
            .ToListAsync(cancellationToken);

        return assignments;
    }
}
