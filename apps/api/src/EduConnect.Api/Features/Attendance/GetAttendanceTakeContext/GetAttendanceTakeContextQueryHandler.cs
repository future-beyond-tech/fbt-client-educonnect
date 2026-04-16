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
            throw new ForbiddenException("Only teachers can take attendance.");
        }

        var isClassTeacher = await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.ClassId == request.ClassId &&
                tca.IsClassTeacher,
                cancellationToken);

        if (!isClassTeacher)
        {
            throw new ForbiddenException("Only the class teacher can take attendance for this class.");
        }

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
            students,
            exceptions,
            approved,
            pending);
    }
}

