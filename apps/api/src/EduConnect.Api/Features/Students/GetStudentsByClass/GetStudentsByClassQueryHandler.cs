using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Models;
using EduConnect.Api.Infrastructure.Database;

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

        // TEACHER ROLE: Must provide classId OR auto-scope to assigned classes
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

            // If teacher provides a classId, verify they are assigned to it
            if (request.ClassId.HasValue)
            {
                if (!assignedClassIds.Contains(request.ClassId.Value))
                {
                    throw new ForbiddenException("You can only view students in your assigned classes.");
                }

                query = query.Where(s => s.ClassId == request.ClassId.Value);
            }
            else
            {
                // No classId provided — scope to all assigned classes
                query = query.Where(s => assignedClassIds.Contains(s.ClassId));
            }

            // Teachers only see active students
            query = query.Where(s => s.IsActive);
        }
        // ADMIN ROLE: Optional classId filter, sees all students including inactive
        else if (_currentUserService.Role == "Admin")
        {
            if (request.ClassId.HasValue)
            {
                query = query.Where(s => s.ClassId == request.ClassId.Value);
            }
        }
        // PARENT ROLE: Not allowed to use this endpoint
        else
        {
            throw new ForbiddenException("Parents should use the my-children endpoint.");
        }

        // Search by name or roll number
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchLower) ||
                s.RollNumber.ToLower().Contains(searchLower));
        }

        // Clamp page size
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Class!.Name)
            .ThenBy(s => s.Class!.Section)
            .ThenBy(s => s.RollNumber)
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
                s.DateOfBirth))
            .ToListAsync(cancellationToken);

        return PagedResult<StudentListDto>.Create(items, totalCount, page, pageSize);
    }
}
