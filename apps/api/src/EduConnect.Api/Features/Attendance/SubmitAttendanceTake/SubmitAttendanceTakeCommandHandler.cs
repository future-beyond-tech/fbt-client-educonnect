using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attendance.SubmitAttendanceTake;

public class SubmitAttendanceTakeCommandHandler
    : IRequestHandler<SubmitAttendanceTakeCommand, SubmitAttendanceTakeResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SubmitAttendanceTakeCommandHandler> _logger;

    public SubmitAttendanceTakeCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<SubmitAttendanceTakeCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<SubmitAttendanceTakeResponse> Handle(
        SubmitAttendanceTakeCommand request,
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

        var studentIds = request.Items.Select(i => i.StudentId).Distinct().ToList();

        // Ensure all targeted students belong to this class
        var validStudentIds = await _context.Students
            .Where(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.ClassId == request.ClassId &&
                s.IsActive &&
                studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = studentIds.Except(validStudentIds).ToList();
        if (invalidIds.Count > 0)
        {
            throw new ForbiddenException("One or more students are not in this class.");
        }

        // Approved leaves overlapping this date are excused (no attendance exception allowed)
        var excusedStudentIds = await _context.LeaveApplications
            .Where(la =>
                la.SchoolId == _currentUserService.SchoolId &&
                !la.IsDeleted &&
                la.Status == "Approved" &&
                validStudentIds.Contains(la.StudentId) &&
                la.StartDate <= request.Date &&
                la.EndDate >= request.Date)
            .Select(la => la.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var attemptsOnExcused = request.Items
            .Where(i => excusedStudentIds.Contains(i.StudentId) &&
                        !string.Equals(i.Status, "Present", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.StudentId)
            .Distinct()
            .ToList();

        if (attemptsOnExcused.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Items", new[] { "Cannot mark absent/late for students with an approved leave on this date." } },
            });
        }

        var existing = await _context.AttendanceRecords
            .Where(ar =>
                ar.SchoolId == _currentUserService.SchoolId &&
                !ar.IsDeleted &&
                ar.Date == request.Date &&
                validStudentIds.Contains(ar.StudentId))
            .ToListAsync(cancellationToken);

        var existingByStudentId = existing.ToDictionary(x => x.StudentId, x => x);

        var created = 0;
        var updated = 0;
        var cleared = 0;

        var createdRecords = new List<AttendanceRecordEntity>();

        foreach (var item in request.Items)
        {
            var normalizedStatus = item.Status.Trim();

            if (string.Equals(normalizedStatus, "Present", StringComparison.OrdinalIgnoreCase))
            {
                if (existingByStudentId.TryGetValue(item.StudentId, out var record))
                {
                    record.IsDeleted = true;
                    record.DeletedAt = DateTimeOffset.UtcNow;
                    cleared++;
                }

                continue;
            }

            // Absent/Late
            if (existingByStudentId.TryGetValue(item.StudentId, out var existingRecord))
            {
                existingRecord.Status = normalizedStatus.Equals("Late", StringComparison.OrdinalIgnoreCase) ? "Late" : "Absent";
                existingRecord.Reason = string.IsNullOrWhiteSpace(item.Reason) ? null : item.Reason.Trim();
                existingRecord.EnteredById = _currentUserService.UserId;
                existingRecord.EnteredByRole = _currentUserService.Role;
                updated++;
            }
            else
            {
                var newRecord = new AttendanceRecordEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = _currentUserService.SchoolId,
                    StudentId = item.StudentId,
                    Date = request.Date,
                    Status = normalizedStatus.Equals("Late", StringComparison.OrdinalIgnoreCase) ? "Late" : "Absent",
                    Reason = string.IsNullOrWhiteSpace(item.Reason) ? null : item.Reason.Trim(),
                    EnteredById = _currentUserService.UserId,
                    EnteredByRole = _currentUserService.Role,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                _context.AttendanceRecords.Add(newRecord);
                createdRecords.Add(newRecord);
                created++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify parents for newly-created absence/late records
        if (createdRecords.Count > 0)
        {
            var createdStudentIds = createdRecords.Select(r => r.StudentId).Distinct().ToList();

            var studentInfo = await _context.Students
                .Where(s => s.SchoolId == _currentUserService.SchoolId && createdStudentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(cancellationToken);

            var nameByStudentId = studentInfo.ToDictionary(x => x.Id, x => x.Name);

            var parentLinks = await _context.ParentStudentLinks
                .Where(psl =>
                    psl.SchoolId == _currentUserService.SchoolId &&
                    createdStudentIds.Contains(psl.StudentId))
                .Select(psl => new { psl.StudentId, psl.ParentId })
                .ToListAsync(cancellationToken);

            var parentIdsByStudent = parentLinks
                .GroupBy(x => x.StudentId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ParentId).Distinct().ToList());

            foreach (var record in createdRecords)
            {
                if (!parentIdsByStudent.TryGetValue(record.StudentId, out var parentIds) || parentIds.Count == 0)
                {
                    continue;
                }

                var studentName = nameByStudentId.TryGetValue(record.StudentId, out var n) ? n : "a student";
                var title = record.Status == "Late" ? $"Late: {studentName}" : $"Absence: {studentName}";
                var body = record.Status == "Late"
                    ? $"{studentName} was marked late on {record.Date:yyyy-MM-dd}."
                    : $"{studentName} was marked absent on {record.Date:yyyy-MM-dd}.";

                if (!string.IsNullOrWhiteSpace(record.Reason))
                {
                    body += $" Reason: {record.Reason}";
                }

                await _notificationService.SendBatchAsync(
                    _currentUserService.SchoolId,
                    parentIds,
                    "absence_marked",
                    title,
                    body,
                    record.Id,
                    "attendance",
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Attendance taken for class {ClassId} on {Date} by teacher {TeacherId}: created={Created} updated={Updated} cleared={Cleared}",
            request.ClassId, request.Date, _currentUserService.UserId, created, updated, cleared);

        return new SubmitAttendanceTakeResponse(
            created,
            updated,
            cleared,
            "Attendance saved successfully.");
    }
}

