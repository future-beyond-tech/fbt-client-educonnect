using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public class GetAttachmentsForEntityQueryHandler : IRequestHandler<GetAttachmentsForEntityQuery, List<AttachmentDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;

    public GetAttachmentsForEntityQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
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

        var attachments = await _context.Attachments
            .Where(a =>
                a.EntityId == request.EntityId &&
                a.EntityType == request.EntityType &&
                a.SchoolId == _currentUserService.SchoolId)
            .OrderBy(a => a.UploadedAt)
            .ToListAsync(cancellationToken);

        var result = new List<AttachmentDto>(attachments.Count);

        foreach (var attachment in attachments)
        {
            var downloadUrl = await _storageService.GeneratePresignedDownloadUrlAsync(
                attachment.StorageKey,
                TimeSpan.FromHours(1),
                attachment.FileName,
                attachment.ContentType,
                AttachmentFeatureRules.RequiresForcedDownload(attachment.ContentType),
                cancellationToken);

            result.Add(new AttachmentDto(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                downloadUrl,
                attachment.UploadedAt));
        }

        return result;
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
}
