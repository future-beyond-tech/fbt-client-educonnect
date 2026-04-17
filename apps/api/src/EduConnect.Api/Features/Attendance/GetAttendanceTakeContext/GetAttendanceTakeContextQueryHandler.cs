using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Attendance.GetAttendanceTakeContext;

public class GetAttendanceTakeContextQueryHandler
    : IRequestHandler<GetAttendanceTakeContextQuery, GetAttendanceTakeContextResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetAttendanceTakeContextQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GetAttendanceTakeContextResponse> Handle(
        GetAttendanceTakeContextQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can view class attendance.");
        }

        // Any teacher assigned to the class (class teacher OR subject teacher)
        // may VIEW attendance. Only class teachers may edit — that check stays
        // in SubmitAttendanceTakeCommandHandler, and is surfaced to the client
        // via the CanEdit flag on the response.
        var assignments = await _context.TeacherClassAssignments
            .Where(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.ClassId == request.ClassId)
            .Select(tca => new { tca.IsClassTeacher })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            throw new ForbiddenException("You are not assigned to this class.");
        }

        var canEdit = assignments.Any(a => a.IsClassTeacher);

        var students = await _context.Students
            .Where(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.ClassId == request.ClassId &&
                s.IsActive)
            .OrderBy(s => s.RollNumber)
            .Select(s => new TakeStudentDto(s.Id, s.Name, s.RollNumber))
            .ToListAsync(cancellationToken);

        var studentIds = students.Select(s => s.Id).ToList();

        var exceptions = await _context.AttendanceRecords
            .Where(ar =>
                ar.SchoolId == _currentUserService.SchoolId &&
                !ar.IsDeleted &&
                ar.Date == request.Date &&
                studentIds.Contains(ar.StudentId))
            .Select(ar => new TakeExceptionDto(ar.StudentId, ar.Status, ar.Reason))
            .ToListAsync(cancellationToken);

        var leavesOverlappingDate = await _context.LeaveApplications
            .Where(la =>
                la.SchoolId == _currentUserService.SchoolId &&
                !la.IsDeleted &&
                studentIds.Contains(la.StudentId) &&
                la.StartDate <= request.Date &&
                la.EndDate >= request.Date &&
                (la.Status == "Pending" || la.Status == "Approved"))
            .OrderByDescending(la => la.CreatedAt)
            .Select(la => new TakeLeaveDto(
                la.Id,
                la.StudentId,
                la.Student != null ? la.Student.Name : string.Empty,
                la.Student != null ? la.Student.RollNumber : string.Empty,
                la.StartDate,
                la.EndDate,
                la.Reason,
                la.Status))
            .ToListAsync(cancellationToken);

        var approved = leavesOverlappingDate.Where(l => l.Status == "Approved").ToList();
        var pending = leavesOverlappingDate.Where(l => l.Status == "Pending").ToList();

        return new GetAttendanceTakeContextResponse(
            request.ClassId,
            request.Date,
            canEdit,
            students,
            exceptions,
            approved,
            pending);
    }
}

