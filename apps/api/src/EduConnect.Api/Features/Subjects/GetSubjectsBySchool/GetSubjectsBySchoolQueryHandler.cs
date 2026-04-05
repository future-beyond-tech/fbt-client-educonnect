using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Subjects.GetSubjectsBySchool;

public class GetSubjectsBySchoolQueryHandler : IRequestHandler<GetSubjectsBySchoolQuery, List<SubjectDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetSubjectsBySchoolQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<SubjectDto>> Handle(GetSubjectsBySchoolQuery request, CancellationToken cancellationToken)
    {
        var subjects = await _context.Subjects
            .Where(s => s.SchoolId == _currentUserService.SchoolId)
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name))
            .ToListAsync(cancellationToken);

        return subjects;
    }
}
