using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attendance.ApproveLeave;

public class ApproveLeaveCommandHandler : IRequestHandler<ApproveLeaveCommand, ApproveLeaveResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApproveLeaveCommandHandler> _logger;

    public ApproveLeaveCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<ApproveLeaveCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApproveLeaveResponse> Handle(ApproveLeaveCommand request, CancellationToken cancellationToken)
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
            throw new ForbiddenException("Only pending leave requests can be approved.");
        }

        await EnsureCanReviewAsync(leave.StudentId, cancellationToken);

        leave.Status = "Approved";
        leave.ReviewedById = _currentUserService.UserId;
        leave.ReviewedAt = DateTimeOffset.UtcNow;
        leave.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var studentName = leave.Student?.Name ?? "your child";
        var dateRange = leave.StartDate == leave.EndDate
            ? leave.StartDate.ToString("yyyy-MM-dd")
            : $"{leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd}";

        await _notificationService.SendAsync(
            leave.SchoolId,
            leave.ParentId,
            "leave_approved",
            "Leave Approved",
            $"Your leave request for {studentName} ({dateRange}) has been approved.",
            leave.Id,
            "leave_application",
            cancellationToken);

        _logger.LogInformation(
            "Leave application {LeaveId} approved by reviewer {ReviewerId} ({Role})",
            leave.Id, _currentUserService.UserId, _currentUserService.Role);

        return new ApproveLeaveResponse("Leave request approved.");
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

