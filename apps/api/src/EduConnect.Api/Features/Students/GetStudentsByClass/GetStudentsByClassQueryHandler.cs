using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Models;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Students.GetStudentsByClass;

public class GetStudentsByClassQueryHandler : IRequestHandler<GetStudentsByClassQuery, PagedResult<StudentListDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<GetStudentsByClassQueryHandler> _logger;

    public GetStudentsByClassQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<GetStudentsByClassQueryHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<PagedResult<StudentListDto>> Handle(GetStudentsByClassQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Students
            .Include(s => s.Class)
            .Where(s => s.SchoolId == _currentUserService.SchoolId)
            .AsQueryable();

        var requestedClassIds = ResolveRequestedClassIds(request);

        // TEACHER ROLE: must be inside their assigned classes, active students only.
        if (_currentUserService.Role == "Teacher")
        {
            var assignedClassIds = await _context.TeacherClassAssignments
                .Where(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId)
                .Select(tca => tca.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (assignedClassIds.Count == 0)
            {
                return PagedResult<StudentListDto>.Create([], 0, request.Page, request.PageSize);
            }

            if (requestedClassIds.Count > 0)
            {
                var unauthorised = requestedClassIds.Except(assignedClassIds).ToList();
                if (unauthorised.Count > 0)
                {
                    throw new ForbiddenException("You can only view students in your assigned classes.");
                }
                query = query.Where(s => requestedClassIds.Contains(s.ClassId));
            }
            else
            {
                query = query.Where(s => assignedClassIds.Contains(s.ClassId));
            }

            query = query.Where(s => s.IsActive);
        }
        // ADMIN ROLE: optional class + status filters, sees inactive too.
        else if (_currentUserService.Role == "Admin")
        {
            if (requestedClassIds.Count > 0)
            {
                query = query.Where(s => requestedClassIds.Contains(s.ClassId));
            }

            var status = ParseStatus(request.Status);
            if (status == StudentStatus.Active) query = query.Where(s => s.IsActive);
            else if (status == StudentStatus.Inactive) query = query.Where(s => !s.IsActive);
        }
        // PARENT ROLE: not allowed on this endpoint.
        else
        {
            throw new ForbiddenException("Parents should use the my-children endpoint.");
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchLower) ||
                s.RollNumber.ToLower().Contains(searchLower));
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = ApplyOrdering(query, ParseSortBy(request.SortBy));

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentListDto(
                s.Id,
                s.Name,
                s.RollNumber,
                s.ClassId,
                s.Class!.Name,
                s.Class!.Section,
                s.IsActive,
                s.DateOfBirth,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<StudentListDto>.Create(items, totalCount, page, pageSize);
    }

    private static IOrderedQueryable<StudentEntity> ApplyOrdering(
        IQueryable<StudentEntity> query,
        StudentSortBy sortBy) => sortBy switch
    {
        StudentSortBy.NameAsc => query.OrderBy(s => s.Name).ThenBy(s => s.RollNumber),
        StudentSortBy.NameDesc => query.OrderByDescending(s => s.Name).ThenBy(s => s.RollNumber),
        StudentSortBy.RollAsc => query.OrderBy(s => s.RollNumber),
        StudentSortBy.CreatedDesc => query.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Name),
        // Default: grouped by class+section, then by roll. Matches the pre-filter UX.
        _ => query.OrderBy(s => s.Class!.Name).ThenBy(s => s.Class!.Section).ThenBy(s => s.RollNumber)
    };

    private static List<Guid> ResolveRequestedClassIds(GetStudentsByClassQuery request)
    {
        var ids = new List<Guid>();
        if (request.ClassId.HasValue)
        {
            ids.Add(request.ClassId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.ClassIds))
        {
            foreach (var token in request.ClassIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(token, out var parsed))
                {
                    ids.Add(parsed);
                }
            }
        }
        return ids.Distinct().ToList();
    }

    private static StudentStatus? ParseStatus(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "active" => StudentStatus.Active,
            "inactive" => StudentStatus.Inactive,
            _ => null
        };

    private static StudentSortBy ParseSortBy(string? raw) =>
        raw?.Trim() switch
        {
            "nameAsc" => StudentSortBy.NameAsc,
            "nameDesc" => StudentSortBy.NameDesc,
            "rollAsc" => StudentSortBy.RollAsc,
            "createdDesc" => StudentSortBy.CreatedDesc,
            _ => StudentSortBy.Default
        };

    private enum StudentStatus { Active, Inactive }

    private enum StudentSortBy { Default, NameAsc, NameDesc, RollAsc, CreatedDesc }
}
