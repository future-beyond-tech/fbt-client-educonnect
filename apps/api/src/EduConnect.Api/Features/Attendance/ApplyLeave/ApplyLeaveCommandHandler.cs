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
        // ── 1. Normalize requested student IDs (single + array merged, deduped) ──
        var requestedStudentIds = NormalizeStudentIds(request);
        if (requestedStudentIds.Count == 0)
        {
            // Validator is the primary guard; this is a defensive fallback.
            throw new ValidationException(
                "At least one child must be selected.",
                new Dictionary<string, string[]>
                {
                    ["StudentIds"] = ["Select at least one child to apply leave for."]
                });
        }

        // ── 2. Bulk-verify parent-child links in one DB round trip ──
        // Pulling all links at once avoids N separate lookups when a parent
        // submits leave for multiple children. If ANY requested child is not
        // linked, fail atomically (no partial commits).
        var parentStudentLinks = await _context.ParentStudentLinks
            .Include(psl => psl.Student)
            .Where(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.ParentId == _currentUserService.UserId &&
                requestedStudentIds.Contains(psl.StudentId))
            .ToListAsync(cancellationToken);

        if (parentStudentLinks.Count != requestedStudentIds.Count)
        {
            var linkedIds = parentStudentLinks.Select(l => l.StudentId).ToHashSet();
            var missing = requestedStudentIds.Where(id => !linkedIds.Contains(id)).ToArray();

            _logger.LogWarning(
                "Parent {ParentId} attempted to apply leave for unlinked student(s) {MissingIds}",
                _currentUserService.UserId, string.Join(",", missing));

            throw new ForbiddenException(
                "You do not have permission to apply leave for one or more of the selected children.");
        }

        // Preserve requested order for stable response / notifications.
        var linksById = parentStudentLinks.ToDictionary(l => l.StudentId);
        var orderedLinks = requestedStudentIds.Select(id => linksById[id]).ToList();

        // ── 3. Create one LeaveApplicationEntity per child in a single SaveChanges ──
        var now = DateTimeOffset.UtcNow;
        var applications = orderedLinks
            .Select(link => new LeaveApplicationEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                StudentId = link.StudentId,
                ParentId = _currentUserService.UserId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Reason = request.Reason,
                Status = "Pending",
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        _context.LeaveApplications.AddRange(applications);
        await _context.SaveChangesAsync(cancellationToken);

        // ── 4. Notifications — consolidated when possible ──
        // Admins get ONE notification summarizing all children (avoids inbox spam).
        // Class-teachers get one notification per student (scoped to their class).
        await NotifyAdminsAsync(orderedLinks, applications, request, cancellationToken);
        await NotifyClassTeachersAsync(orderedLinks, applications, request, cancellationToken);

        _logger.LogInformation(
            "Leave applications created: parent={ParentId}, count={Count}, students={StudentIds}, ids={LeaveIds}",
            _currentUserService.UserId,
            applications.Count,
            string.Join(",", applications.Select(a => a.StudentId)),
            string.Join(",", applications.Select(a => a.Id)));

        var leaveIds = applications.Select(a => a.Id).ToArray();
        var message = applications.Count == 1
            ? "Leave application submitted successfully."
            : $"Leave application submitted for {applications.Count} children.";

        return new ApplyLeaveResponse(
            leaveIds[0],
            leaveIds,
            applications.Count,
            "Pending",
            message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Merges the legacy <see cref="ApplyLeaveCommand.StudentId"/> and the
    /// preferred <see cref="ApplyLeaveCommand.StudentIds"/> into one deduped,
    /// ordered list. Preserves the order of the first appearance of each ID
    /// so the UI's "selection order" is reflected in the response.
    /// </summary>
    private static IReadOnlyList<Guid> NormalizeStudentIds(ApplyLeaveCommand request)
    {
        var seen = new HashSet<Guid>();
        var ordered = new List<Guid>();

        if (request.StudentIds is { Length: > 0 })
        {
            foreach (var id in request.StudentIds)
            {
                if (id != Guid.Empty && seen.Add(id))
                {
                    ordered.Add(id);
                }
            }
        }

        if (request.StudentId is Guid legacy && legacy != Guid.Empty && seen.Add(legacy))
        {
            ordered.Add(legacy);
        }

        return ordered;
    }

    private async Task NotifyAdminsAsync(
        List<ParentStudentLinkEntity> links,
        List<LeaveApplicationEntity> applications,
        ApplyLeaveCommand request,
        CancellationToken cancellationToken)
    {
        var adminIds = await _context.Users
            .Where(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Role == "Admin" &&
                u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
        {
            return;
        }

        var dateRange = FormatDateRange(request.StartDate, request.EndDate);
        var names = string.Join(", ", links.Select(l => l.Student?.Name ?? "a student"));
        var title = applications.Count == 1
            ? $"Leave Application: {names}"
            : $"Leave Application: {applications.Count} children";
        var body = applications.Count == 1
            ? $"A leave application has been submitted for {names} ({dateRange}). Reason: {request.Reason}"
            : $"A leave application has been submitted for {applications.Count} children — {names} ({dateRange}). Reason: {request.Reason}";

        // Link the notification to the first application; the UI can expand to
        // show sibling applications submitted in the same batch via the parent.
        await _notificationService.SendBatchAsync(
            _currentUserService.SchoolId,
            adminIds,
            "leave_applied",
            title,
            body,
            applications[0].Id,
            "leave_application",
            cancellationToken);
    }

    private async Task NotifyClassTeachersAsync(
        List<ParentStudentLinkEntity> links,
        List<LeaveApplicationEntity> applications,
        ApplyLeaveCommand request,
        CancellationToken cancellationToken)
    {
        // Collect the distinct class IDs we need to fan out to.
        var classIds = links
            .Select(l => l.Student?.ClassId)
            .Where(cid => cid.HasValue)
            .Select(cid => cid!.Value)
            .Distinct()
            .ToArray();

        if (classIds.Length == 0)
        {
            return;
        }

        // Single query to map classId -> class-teacher user IDs.
        var classTeacherAssignments = await _context.TeacherClassAssignments
            .Where(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                classIds.Contains(tca.ClassId) &&
                tca.IsClassTeacher)
            .Select(tca => new { tca.ClassId, tca.TeacherId })
            .ToListAsync(cancellationToken);

        if (classTeacherAssignments.Count == 0)
        {
            return;
        }

        var teachersByClass = classTeacherAssignments
            .GroupBy(a => a.ClassId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.TeacherId).Distinct().ToList());

        var dateRange = FormatDateRange(request.StartDate, request.EndDate);

        // One notification per student — class teachers only care about kids in
        // their class, so they should see the details for that specific child.
        // The applications list is aligned with links, so we can zip them.
        for (var i = 0; i < links.Count; i++)
        {
            var link = links[i];
            var application = applications[i];
            var classId = link.Student?.ClassId;
            if (!classId.HasValue) continue;
            if (!teachersByClass.TryGetValue(classId.Value, out var teacherIds)) continue;
            if (teacherIds.Count == 0) continue;

            var studentName = link.Student?.Name ?? "a student";
            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                teacherIds,
                "leave_applied",
                $"Leave Application: {studentName}",
                $"A leave application has been submitted for {studentName} ({dateRange}). Reason: {request.Reason}",
                application.Id,
                "leave_application",
                cancellationToken);
        }
    }

    private static string FormatDateRange(DateOnly start, DateOnly end) =>
        start == end
            ? start.ToString("yyyy-MM-dd")
            : $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}";
}
