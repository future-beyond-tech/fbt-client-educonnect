using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Models;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

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

        var schoolId = _currentUserService.SchoolId;

        var query = _context.Users
            .Where(u =>
                u.SchoolId == schoolId &&
                (u.Role == "Teacher" || u.Role == "Admin"));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.Trim().ToLower();
            var phoneSearch = JapanPhoneNumber.NormalizeSearchTerm(request.Search);
            query = query.Where(u =>
                u.Name.ToLower().Contains(searchLower) ||
                (!string.IsNullOrEmpty(phoneSearch) && u.Phone.Contains(phoneSearch)));
        }

        var subjectFilter = ParseSubjects(request.Subjects);
        if (subjectFilter.Count > 0)
        {
            query = query.Where(u => _context.TeacherClassAssignments
                .Any(tca => tca.TeacherId == u.Id
                    && tca.SchoolId == schoolId
                    && subjectFilter.Contains(tca.Subject)));
        }

        var classLoad = ParseClassLoad(request.ClassLoad);
        if (classLoad.HasValue)
        {
            query = classLoad.Value switch
            {
                ClassLoadBucket.Unassigned => query.Where(u => !_context.TeacherClassAssignments
                    .Any(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)),
                ClassLoadBucket.Light => query.Where(u =>
                    _context.TeacherClassAssignments
                        .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                        .Select(tca => tca.ClassId).Distinct().Count() >= 1 &&
                    _context.TeacherClassAssignments
                        .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                        .Select(tca => tca.ClassId).Distinct().Count() <= 2),
                ClassLoadBucket.Heavy => query.Where(u =>
                    _context.TeacherClassAssignments
                        .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                        .Select(tca => tca.ClassId).Distinct().Count() >= 3),
                _ => query
            };
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = ApplyOrdering(query, ParseSortBy(request.SortBy), schoolId);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new TeacherListDto(
                u.Id,
                u.Name,
                u.Phone,
                u.Role,
                u.IsActive,
                _context.TeacherClassAssignments
                    .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                    .Select(tca => tca.ClassId).Distinct().Count(),
                _context.TeacherClassAssignments
                    .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                    .Select(tca => tca.Subject).Distinct().OrderBy(s => s).ToList(),
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<TeacherListDto>.Create(items, totalCount, page, pageSize);
    }

    private IOrderedQueryable<UserEntity> ApplyOrdering(
        IQueryable<UserEntity> query,
        TeacherSortBy sortBy,
        Guid schoolId) => sortBy switch
    {
        TeacherSortBy.NameDesc => query.OrderByDescending(u => u.Name),
        TeacherSortBy.ClassesDesc => query
            .OrderByDescending(u => _context.TeacherClassAssignments
                .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                .Select(tca => tca.ClassId).Distinct().Count())
            .ThenBy(u => u.Name),
        TeacherSortBy.ClassesAsc => query
            .OrderBy(u => _context.TeacherClassAssignments
                .Where(tca => tca.TeacherId == u.Id && tca.SchoolId == schoolId)
                .Select(tca => tca.ClassId).Distinct().Count())
            .ThenBy(u => u.Name),
        TeacherSortBy.CreatedDesc => query.OrderByDescending(u => u.CreatedAt),
        _ => query.OrderBy(u => u.Name)
    };

    private static List<string> ParseSubjects(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static ClassLoadBucket? ParseClassLoad(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "unassigned" => ClassLoadBucket.Unassigned,
            "light" => ClassLoadBucket.Light,
            "heavy" => ClassLoadBucket.Heavy,
            _ => null
        };

    private static TeacherSortBy ParseSortBy(string? raw) =>
        raw?.Trim() switch
        {
            "nameDesc" => TeacherSortBy.NameDesc,
            "classesDesc" => TeacherSortBy.ClassesDesc,
            "classesAsc" => TeacherSortBy.ClassesAsc,
            "createdDesc" => TeacherSortBy.CreatedDesc,
            _ => TeacherSortBy.NameAsc
        };

    private enum ClassLoadBucket { Unassigned, Light, Heavy }

    private enum TeacherSortBy { NameAsc, NameDesc, ClassesDesc, ClassesAsc, CreatedDesc }
}
