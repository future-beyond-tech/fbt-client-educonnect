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
        // Step 1: Resolve student by roll number within this school
        var student = await _context.Students
            .FirstOrDefaultAsync(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.RollNumber == request.RollNumber &&
                s.IsActive,
                cancellationToken);

        if (student == null)
        {
            _logger.LogWarning(
                "Student with roll number {RollNumber} not found in school {SchoolId}",
                request.RollNumber, _currentUserService.SchoolId);
            throw new NotFoundException($"Student with roll number '{request.RollNumber}' not found.");
        }

        // Step 2: Authorize based on the caller's role
        var role = _currentUserService.Role;

        if (role == "Teacher")
        {
            var isAssignedToClass = await _context.TeacherClassAssignments
                .AnyAsync(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId &&
                    tca.ClassId == student.ClassId,
                    cancellationToken);

            if (!isAssignedToClass)
            {
                _logger.LogWarning(
                    "Teacher {TeacherId} attempted to mark absence for student {StudentId} in unassigned class {ClassId}",
                    _currentUserService.UserId, student.Id, student.ClassId);
                throw new ForbiddenException("You are not assigned to this student's class.");
            }
        }
        else if (role == "Parent")
        {
            var isLinked = await _context.ParentStudentLinks
                .AnyAsync(psl =>
                    psl.SchoolId == _currentUserService.SchoolId &&
                    psl.ParentId == _currentUserService.UserId &&
                    psl.StudentId == student.Id,
                    cancellationToken);

            if (!isLinked)
            {
                _logger.LogWarning(
                    "Parent {ParentId} attempted to mark absence for student {StudentId} they are not linked to",
                    _currentUserService.UserId, student.Id);
                throw new ForbiddenException("You do not have permission to mark attendance for this student.");
            }
        }
        else
        {
            throw new ForbiddenException("Only teachers and parents can mark student absences.");
        }

        // Step 3: Guard against duplicate records on the same date
        var existingRecord = await _context.AttendanceRecords
            .FirstOrDefaultAsync(ar =>
                ar.SchoolId == _currentUserService.SchoolId &&
                ar.StudentId == student.Id &&
                ar.Date == request.Date &&
                !ar.IsDeleted,
                cancellationToken);

        if (existingRecord != null)
        {
            _logger.LogWarning(
                "Duplicate attendance record attempt for student {StudentId} on date {Date}",
                student.Id, request.Date);
            throw new InvalidOperationException("Attendance record already exists for this student on this date.");
        }

        // Step 4: Persist the absence record
        var attendanceRecord = new AttendanceRecordEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            StudentId = student.Id,
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

        // Step 5: Notify all parents linked to this student
        var allParentIds = await _context.ParentStudentLinks
            .Where(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.StudentId == student.Id)
            .Select(psl => psl.ParentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (allParentIds.Count > 0)
        {
            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                allParentIds,
                "absence_marked",
                $"Absence: {student.Name}",
                $"{student.Name} was marked absent on {request.Date:yyyy-MM-dd}." +
                    (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Reason: {request.Reason}"),
                attendanceRecord.Id,
                "attendance",
                cancellationToken);
        }

        _logger.LogInformation(
            "Attendance record {RecordId} created for student {StudentId} (roll: {RollNumber}) by {UserId} ({Role})",
            attendanceRecord.Id, student.Id, request.RollNumber, _currentUserService.UserId, role);

        return new MarkAbsenceResponse(attendanceRecord.Id, "Absent", "Absence marked successfully.");
    }
}
