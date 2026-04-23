using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using EduConnect.Api.Infrastructure.Services.Scanning;
using MediatR;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public class GetAttachmentsForEntityQueryHandler : IRequestHandler<GetAttachmentsForEntityQuery, List<AttachmentDto>>
{
    // Compliance-grade audit logging is required for downloads of student
    // submissions and official notices, so the response routes those
    // download URLs through the API's audit-logging redirect endpoint.
    // Homework attachments stay on direct presigned URLs — the surface is
    // teacher-facing and per-download auditing isn't worth the extra hop.
    private static readonly HashSet<string> EntityTypesRequiringAuditRedirect =
        new(StringComparer.OrdinalIgnoreCase) { "homework_submission", "notice" };

    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly StorageOptions _storageOptions;

    public GetAttachmentsForEntityQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService,
        IOptions<StorageOptions> storageOptions)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _storageOptions = storageOptions.Value;
    }

    public async Task<List<AttachmentDto>> Handle(GetAttachmentsForEntityQuery request, CancellationToken cancellationToken)
    {
        if (request.EntityType == "homework")
        {
            await EnsureHomeworkAccessAsync(request.EntityId, cancellationToken);
        }
        else if (request.EntityType == "notice")
        {
            await EnsureNoticeAccessAsync(request.EntityId, cancellationToken);
        }
        else if (request.EntityType == "homework_submission")
        {
            await EnsureHomeworkSubmissionAccessAsync(request.EntityId, cancellationToken);
        }

        // Status visibility (Phase 4 remediation):
        //  - Admin / Teacher: see Available + Pending + ScanFailed so the
        //    UI can show "scanning…" / "blocked" badges. Infected rows
        //    stay hidden — admins are notified out-of-band (Phase 6).
        //  - Parent / everyone else: only Available, so a child- or
        //    parent-facing surface never hints at a scan in progress.
        // Presigned URLs are only minted for Available rows regardless of
        // role, so the read path can never hand out an unscanned object.
        var visibleStatuses = CanViewInProgressScans(_currentUserService.Role)
            ? new[]
            {
                AttachmentStatus.Available,
                AttachmentStatus.Pending,
                AttachmentStatus.ScanFailed,
            }
            : new[] { AttachmentStatus.Available };

        var attachments = await _context.Attachments
            .Where(a =>
                a.EntityId == request.EntityId &&
                a.EntityType == request.EntityType &&
                a.SchoolId == _currentUserService.SchoolId &&
                visibleStatuses.Contains(a.Status))
            .OrderBy(a => a.UploadedAt)
            .ToListAsync(cancellationToken);

        var result = new List<AttachmentDto>(attachments.Count);

        var routeThroughAudit = EntityTypesRequiringAuditRedirect.Contains(request.EntityType);

        foreach (var attachment in attachments)
        {
            string? downloadUrl = null;
            if (attachment.Status == AttachmentStatus.Available)
            {
                downloadUrl = routeThroughAudit
                    ? BuildAuditRedirectUrl(attachment.Id)
                    : await _storageService.GeneratePresignedDownloadUrlAsync(
                        attachment.StorageKey,
                        TimeSpan.FromMinutes(_storageOptions.PresignedDownloadExpiryMinutes),
                        attachment.FileName,
                        attachment.ContentType,
                        AttachmentFeatureRules.RequiresForcedDownload(attachment.ContentType),
                        cancellationToken);
            }

            // Admin-only hint so the UI can distinguish "scanner not wired
            // up in this env" from a real scan error. Parents never see
            // ScanFailed rows, so this stays null for them.
            string? scanFailureReason = null;
            if (attachment.Status == AttachmentStatus.ScanFailed &&
                _currentUserService.Role == "Admin")
            {
                scanFailureReason = attachment.ThreatName == NoOpAttachmentScanner.NoOpThreatName
                    ? "scanner-unavailable"
                    : "scan-verdict-error";
            }

            result.Add(new AttachmentDto(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                downloadUrl,
                attachment.UploadedAt,
                attachment.Status,
                scanFailureReason));
        }

        return result;
    }

    private static bool CanViewInProgressScans(string? role) =>
        role == "Admin" || role == "Teacher";

    private static string BuildAuditRedirectUrl(Guid attachmentId)
    {
        // Always a RELATIVE URL. The frontend renders this string as an
        // anchor `href`; the browser resolves it against the current
        // document origin (the Next.js web origin), which hits the
        // same-origin proxy at apps/web/app/api/attachments/[id]/download/
        // route.ts. That proxy reads the refresh cookie (set on the web
        // origin only), mints a Bearer access token, and forwards to this
        // API with Authorization.
        //
        // Do NOT return an absolute URL to the API host. Doing so makes
        // the browser navigate directly to the API origin, which has no
        // refresh cookie, no Authorization header, and therefore always
        // returns 401 to parents. See: parent notice 401 incident,
        // 2026-04.
        return $"/api/attachments/{attachmentId}/download";
    }

    private async Task EnsureHomeworkAccessAsync(Guid homeworkId, CancellationToken cancellationToken)
    {
        var homework = await _context.Homeworks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                h => h.Id == homeworkId &&
                     h.SchoolId == _currentUserService.SchoolId &&
                     !h.IsDeleted,
                cancellationToken);

        if (homework == null)
        {
            throw new NotFoundException("Homework", homeworkId.ToString());
        }

        if (_currentUserService.Role == "Admin")
        {
            return;
        }

        if (_currentUserService.Role == "Parent")
        {
            var linkedClassIds = await _context.ParentStudentLinks
                .Where(link =>
                    link.SchoolId == _currentUserService.SchoolId &&
                    link.ParentId == _currentUserService.UserId)
                .Join(_context.Students, link => link.StudentId, student => student.Id, (_, student) => student.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (homework.Status == "Published" && linkedClassIds.Contains(homework.ClassId))
            {
                return;
            }

            throw new ForbiddenException("You do not have access to these homework attachments.");
        }

        if (_currentUserService.Role == "Teacher")
        {
            if (homework.AssignedById == _currentUserService.UserId)
            {
                return;
            }

            var assignedClassIds = await _context.TeacherClassAssignments
                .Where(assignment =>
                    assignment.SchoolId == _currentUserService.SchoolId &&
                    assignment.TeacherId == _currentUserService.UserId)
                .Select(assignment => assignment.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (homework.Status == "Published" && assignedClassIds.Contains(homework.ClassId))
            {
                return;
            }

            var classTeacherClassIds = await _context.TeacherClassAssignments
                .Where(assignment =>
                    assignment.SchoolId == _currentUserService.SchoolId &&
                    assignment.TeacherId == _currentUserService.UserId &&
                    assignment.IsClassTeacher)
                .Select(assignment => assignment.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (homework.Status == "PendingApproval" && classTeacherClassIds.Contains(homework.ClassId))
            {
                return;
            }
        }

        throw new ForbiddenException("You do not have access to these homework attachments.");
    }

    private async Task EnsureNoticeAccessAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        var notice = await _context.Notices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                n => n.Id == noticeId &&
                     n.SchoolId == _currentUserService.SchoolId &&
                     !n.IsDeleted,
                cancellationToken);

        if (notice == null)
        {
            throw new NotFoundException("Notice", noticeId.ToString());
        }

        if (_currentUserService.Role == "Admin" || _currentUserService.Role == "Teacher")
        {
            return;
        }

        if (_currentUserService.Role != "Parent")
        {
            throw new ForbiddenException("You do not have access to these notice attachments.");
        }

        if (!notice.IsPublished)
        {
            throw new ForbiddenException("You do not have access to these notice attachments.");
        }

        if (notice.ExpiresAt.HasValue && notice.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new ForbiddenException("You do not have access to these notice attachments.");
        }

        if (string.Equals(notice.TargetAudience, "All", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var studentClassIds = await _context.ParentStudentLinks
            .Where(link =>
                link.SchoolId == _currentUserService.SchoolId &&
                link.ParentId == _currentUserService.UserId)
            .Join(_context.Students, link => link.StudentId, student => student.Id, (_, student) => student.ClassId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var targetClassIds = await _context.NoticeTargetClasses
            .Where(targetClass =>
                targetClass.SchoolId == _currentUserService.SchoolId &&
                targetClass.NoticeId == notice.Id)
            .Select(targetClass => targetClass.ClassId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (targetClassIds.Any(targetClassId => studentClassIds.Contains(targetClassId)))
        {
            return;
        }

        throw new ForbiddenException("You do not have access to these notice attachments.");
    }

    private async Task EnsureHomeworkSubmissionAccessAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await _context.HomeworkSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == submissionId &&
                     s.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (submission is null)
        {
            throw new NotFoundException("HomeworkSubmission", submissionId.ToString());
        }

        if (_currentUserService.Role == "Admin")
        {
            return;
        }

        if (_currentUserService.Role == "Parent")
        {
            var isLinked = await _context.ParentStudentLinks
                .AnyAsync(link =>
                    link.SchoolId == _currentUserService.SchoolId &&
                    link.ParentId == _currentUserService.UserId &&
                    link.StudentId == submission.StudentId,
                    cancellationToken);

            if (isLinked)
            {
                return;
            }

            throw new ForbiddenException("You do not have access to these submission attachments.");
        }

        if (_currentUserService.Role == "Teacher")
        {
            var homework = await _context.Homeworks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    h => h.Id == submission.HomeworkId &&
                         h.SchoolId == _currentUserService.SchoolId,
                    cancellationToken);

            if (homework is not null)
            {
                if (homework.AssignedById == _currentUserService.UserId)
                {
                    return;
                }

                var assignedClassIds = await _context.TeacherClassAssignments
                    .Where(assignment =>
                        assignment.SchoolId == _currentUserService.SchoolId &&
                        assignment.TeacherId == _currentUserService.UserId)
                    .Select(assignment => assignment.ClassId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (assignedClassIds.Contains(homework.ClassId))
                {
                    return;
                }
            }
        }

        throw new ForbiddenException("You do not have access to these submission attachments.");
    }
}
