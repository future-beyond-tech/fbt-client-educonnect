using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Classes.GetClassesBySchool;

public class GetClassesBySchoolQueryHandler : IRequestHandler<GetClassesBySchoolQuery, List<ClassDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetClassesBySchoolQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ClassDto>> Handle(GetClassesBySchoolQuery request, CancellationToken cancellationToken)
    {
        var classes = await _context.Classes
            .Where(c => c.SchoolId == _currentUserService.SchoolId)
            .Select(c => new ClassDto(
                c.Id,
                c.Name,
                c.Section,
                c.AcademicYear,
                c.Students.Count(s => s.IsActive)))
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Section)
            .ToListAsync(cancellationToken);

        return classes;
    }
}
