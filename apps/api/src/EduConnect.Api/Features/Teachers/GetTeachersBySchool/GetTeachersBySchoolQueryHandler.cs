using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Models;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Teachers.GetTeachersBySchool;

public class GetTeachersBySchoolQueryHandler : IRequestHandler<GetTeachersBySchoolQuery, PagedResult<TeacherListDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetTeachersBySchoolQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<TeacherListDto>> Handle(GetTeachersBySchoolQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can list all teachers.");
        }

        var query = _context.Users
            .Where(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                (u.Role == "Teacher" || u.Role == "Admin"))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.Trim().ToLower();
            var phoneSearch = JapanPhoneNumber.NormalizeSearchTerm(request.Search);
            query = query.Where(u =>
                u.Name.ToLower().Contains(searchLower) ||
                (!string.IsNullOrEmpty(phoneSearch) && u.Phone.Contains(phoneSearch)));
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var totalCount = await query.CountAsync(cancellationToken);

        var teachers = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Phone,
                u.Role,
                u.IsActive,
                Assignments = _context.TeacherClassAssignments
                    .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == _currentUserService.SchoolId)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = teachers.Select(t => new TeacherListDto(
            t.Id,
            t.Name,
            t.Phone,
            t.Role,
            t.IsActive,
            t.Assignments.Select(a => a.ClassId).Distinct().Count(),
            t.Assignments.Select(a => a.Subject).Distinct().OrderBy(s => s).ToList()
        )).ToList();

        return PagedResult<TeacherListDto>.Create(items, totalCount, page, pageSize);
    }
}
