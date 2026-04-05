using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attendance.MarkAbsence;

public class MarkAbsenceCommandHandler : IRequestHandler<MarkAbsenceCommand, MarkAbsenceResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MarkAbsenceCommandHandler> _logger;

    public MarkAbsenceCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<MarkAbsenceCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<MarkAbsenceResponse> Handle(MarkAbsenceCommand request, CancellationToken cancellationToken)
    {
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
                "Parent {ParentId} attempted to mark attendance for student {StudentId} they don't own",
                _currentUserService.UserId, request.StudentId);
            throw new ForbiddenException("You do not have permission to mark attendance for this student.");
        }

        var existingRecord = await _context.AttendanceRecords
            .FirstOrDefaultAsync(ar =>
                ar.SchoolId == _currentUserService.SchoolId &&
                ar.StudentId == request.StudentId &&
                ar.Date == request.Date &&
                !ar.IsDeleted,
                cancellationToken);

        if (existingRecord != null)
        {
            _logger.LogWarning(
                "Duplicate attendance record attempt for student {StudentId} on date {Date}",
                request.StudentId, request.Date);
            throw new InvalidOperationException("Attendance record already exists for this student on this date.");
        }

        var attendanceRecord = new AttendanceRecordEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            StudentId = request.StudentId,
            Date = request.Date,
            Status = "Absent",
            Reason = request.Reason,
            EnteredById = _currentUserService.UserId,
            EnteredByRole = _currentUserService.Role,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.AttendanceRecords.Add(attendanceRecord);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify all parents linked to this student about the absence
        var studentName = parentStudentLink.Student?.Name ?? "your child";
        var allParentIds = await _context.ParentStudentLinks
            .Where(psl => psl.SchoolId == _currentUserService.SchoolId
                && psl.StudentId == request.StudentId)
            .Select(psl => psl.ParentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (allParentIds.Count > 0)
        {
            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                allParentIds,
                "absence_marked",
                $"Absence: {studentName}",
                $"{studentName} was marked absent on {request.Date:yyyy-MM-dd}." +
                    (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Reason: {request.Reason}"),
                attendanceRecord.Id,
                "attendance",
                cancellationToken);
        }

        _logger.LogInformation(
            "Attendance record created: {RecordId} for student {StudentId} by {UserId}",
            attendanceRecord.Id, request.StudentId, _currentUserService.UserId);

        return new MarkAbsenceResponse(attendanceRecord.Id, "Absent", "Absence marked successfully.");
    }
}
