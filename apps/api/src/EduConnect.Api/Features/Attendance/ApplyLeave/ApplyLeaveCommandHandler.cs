using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attendance.ApplyLeave;

public class ApplyLeaveCommandHandler : IRequestHandler<ApplyLeaveCommand, ApplyLeaveResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApplyLeaveCommandHandler> _logger;

    public ApplyLeaveCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<ApplyLeaveCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApplyLeaveResponse> Handle(ApplyLeaveCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify the caller is the parent of the requested student
        var parentStudentLink = await _context.ParentStudentLinks
            .Include(psl => psl.Student)
            .FirstOrDefaultAsync(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.ParentId == _currentUserService.UserId &&
                psl.StudentId == request.StudentId,
                cancellationToken);

        if (parentStudentLink == null)
        {
            _logger.LogWarning(
                "Parent {ParentId} attempted to apply leave for student {StudentId} they are not linked to",
                _currentUserService.UserId, request.StudentId);
            throw new ForbiddenException("You do not have permission to apply leave for this student.");
        }

        // 2. Create the leave application
        var leaveApplication = new LeaveApplicationEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            StudentId = request.StudentId,
            ParentId = _currentUserService.UserId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            Status = "Pending",
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.LeaveApplications.Add(leaveApplication);
        await _context.SaveChangesAsync(cancellationToken);

        // 3. Notify all admins of the school
        var studentName = parentStudentLink.Student?.Name ?? "a student";
        var dateRange = request.StartDate == request.EndDate
            ? request.StartDate.ToString("yyyy-MM-dd")
            : $"{request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}";

        var adminIds = await _context.Users
            .Where(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Role == "Admin" &&
                u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count > 0)
        {
            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                adminIds,
                "leave_applied",
                $"Leave Application: {studentName}",
                $"A leave application has been submitted for {studentName} ({dateRange}). Reason: {request.Reason}",
                leaveApplication.Id,
                "leave_application",
                cancellationToken);
        }

        // 4. Notify class teacher(s) assigned to the student's class
        var classId = parentStudentLink.Student?.ClassId;
        if (classId.HasValue)
        {
            var teacherIds = await _context.TeacherClassAssignments
                .Where(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.ClassId == classId.Value &&
                    tca.IsClassTeacher)
                .Select(tca => tca.TeacherId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (teacherIds.Count > 0)
            {
                await _notificationService.SendBatchAsync(
                    _currentUserService.SchoolId,
                    teacherIds,
                    "leave_applied",
                    $"Leave Application: {studentName}",
                    $"A leave application has been submitted for {studentName} ({dateRange}). Reason: {request.Reason}",
                    leaveApplication.Id,
                    "leave_application",
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Leave application {LeaveId} created for student {StudentId} by parent {ParentId}",
            leaveApplication.Id, request.StudentId, _currentUserService.UserId);

        return new ApplyLeaveResponse(
            leaveApplication.Id,
            "Pending",
            "Leave application submitted successfully.");
    }
}
