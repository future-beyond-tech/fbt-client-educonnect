using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attendance.RejectLeave;

public class RejectLeaveCommandHandler : IRequestHandler<RejectLeaveCommand, RejectLeaveResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RejectLeaveCommandHandler> _logger;

    public RejectLeaveCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<RejectLeaveCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<RejectLeaveResponse> Handle(RejectLeaveCommand request, CancellationToken cancellationToken)
    {
        var leave = await _context.LeaveApplications
            .Include(la => la.Student)
            .FirstOrDefaultAsync(la =>
                la.Id == request.LeaveApplicationId &&
                la.SchoolId == _currentUserService.SchoolId &&
                !la.IsDeleted,
                cancellationToken);

        if (leave == null)
        {
            throw new NotFoundException("LeaveApplication", request.LeaveApplicationId.ToString());
        }

        if (leave.Status != "Pending")
        {
            throw new ForbiddenException("Only pending leave requests can be rejected.");
        }

        await EnsureCanReviewAsync(leave.StudentId, cancellationToken);

        leave.Status = "Rejected";
        leave.ReviewedById = _currentUserService.UserId;
        leave.ReviewedAt = DateTimeOffset.UtcNow;
        leave.ReviewNote = request.ReviewNote.Trim();
        leave.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var studentName = leave.Student?.Name ?? "your child";
        var dateRange = leave.StartDate == leave.EndDate
            ? leave.StartDate.ToString("yyyy-MM-dd")
            : $"{leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd}";

        await _notificationService.SendAsync(
            leave.SchoolId,
            leave.ParentId,
            "leave_rejected",
            "Leave Rejected",
            $"Your leave request for {studentName} ({dateRange}) was rejected. Note: {leave.ReviewNote}",
            leave.Id,
            "leave_application",
            cancellationToken);

        _logger.LogInformation(
            "Leave application {LeaveId} rejected by reviewer {ReviewerId} ({Role})",
            leave.Id, _currentUserService.UserId, _currentUserService.Role);

        return new RejectLeaveResponse("Leave request rejected.");
    }

    private async Task EnsureCanReviewAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role == "Admin")
        {
            return;
        }

        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("You do not have permission to review leave requests.");
        }

        var classId = await _context.Students
            .Where(s =>
                s.Id == studentId &&
                s.SchoolId == _currentUserService.SchoolId)
            .Select(s => (Guid?)s.ClassId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!classId.HasValue)
        {
            throw new NotFoundException("Student", studentId.ToString());
        }

        var isClassTeacher = await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.ClassId == classId.Value &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.IsClassTeacher,
                cancellationToken);

        if (!isClassTeacher)
        {
            throw new ForbiddenException("Only the class teacher can approve or reject leave for this class.");
        }
    }
}

