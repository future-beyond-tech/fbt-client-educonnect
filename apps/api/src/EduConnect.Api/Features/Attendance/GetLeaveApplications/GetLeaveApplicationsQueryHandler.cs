using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Attendance.GetLeaveApplications;

public class GetLeaveApplicationsQueryHandler
    : IRequestHandler<GetLeaveApplicationsQuery, GetLeaveApplicationsResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetLeaveApplicationsQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GetLeaveApplicationsResponse> Handle(
        GetLeaveApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.LeaveApplications
            .Where(la => la.SchoolId == _currentUserService.SchoolId && !la.IsDeleted)
            .AsQueryable();

        // Role-based scoping
        if (_currentUserService.Role == "Parent")
        {
            // Parents see only leaves for their linked students
            var linkedStudentIds = await _context.ParentStudentLinks
                .Where(psl =>
                    psl.SchoolId == _currentUserService.SchoolId &&
                    psl.ParentId == _currentUserService.UserId)
                .Select(psl => psl.StudentId)
                .ToListAsync(cancellationToken);

            query = query.Where(la => linkedStudentIds.Contains(la.StudentId));
        }
        else if (_currentUserService.Role == "Teacher")
        {
            // Teachers see leaves for students in their assigned classes
            var assignedClassIds = await _context.TeacherClassAssignments
                .Where(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId)
                .Select(tca => tca.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var classStudentIds = await _context.Students
                .Where(s => assignedClassIds.Contains(s.ClassId))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(la => classStudentIds.Contains(la.StudentId));
        }
        // Admin: no additional scoping — sees all school leaves

        // Optional filters
        if (request.StudentId.HasValue)
        {
            query = query.Where(la => la.StudentId == request.StudentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(la => la.Status == request.Status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(la => la.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(la => new LeaveApplicationDto(
                la.Id,
                la.StudentId,
                la.Student != null ? la.Student.Name : string.Empty,
                la.Student != null ? la.Student.RollNumber : string.Empty,
                la.Student != null && la.Student.Class != null ? la.Student.Class.Name : string.Empty,
                la.StartDate,
                la.EndDate,
                la.Reason,
                la.Status,
                la.ReviewNote,
                la.CreatedAt))
            .ToListAsync(cancellationToken);

        return new GetLeaveApplicationsResponse(items, totalCount, request.PageNumber, request.PageSize);
    }
}
