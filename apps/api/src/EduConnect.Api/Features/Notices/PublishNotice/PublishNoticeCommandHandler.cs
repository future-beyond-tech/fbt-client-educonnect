using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Notices.PublishNotice;

public class PublishNoticeCommandHandler : IRequestHandler<PublishNoticeCommand, PublishNoticeResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PublishNoticeCommandHandler> _logger;

    public PublishNoticeCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<PublishNoticeCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PublishNoticeResponse> Handle(PublishNoticeCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to publish notice",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only administrators can publish notices.");
        }

        var notice = await _context.Notices
            .Include(n => n.TargetClasses)
            .FirstOrDefaultAsync(n =>
                n.Id == request.NoticeId &&
                n.SchoolId == _currentUserService.SchoolId &&
                !n.IsDeleted,
                cancellationToken);

        if (notice == null)
        {
            _logger.LogWarning("Notice {NoticeId} not found", request.NoticeId);
            throw new NotFoundException("Notice", request.NoticeId.ToString());
        }

        if (notice.IsPublished)
        {
            _logger.LogWarning("Attempt to publish already published notice {NoticeId}", request.NoticeId);
            throw new InvalidOperationException("This notice has already been published and is immutable.");
        }

        notice.IsPublished = true;
        notice.PublishedAt = DateTimeOffset.UtcNow;
        notice.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Notify targeted users based on audience
        var targetUserIds = await ResolveTargetUserIds(
            notice.TargetAudience,
            notice.TargetClasses.Select(targetClass => targetClass.ClassId).ToList(),
            cancellationToken);

        if (targetUserIds.Count > 0)
        {
            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                targetUserIds,
                "notice_published",
                $"New Notice: {notice.Title}",
                notice.Body.Length > 200 ? notice.Body[..200] + "…" : notice.Body,
                notice.Id,
                "notice",
                cancellationToken);
        }

        _logger.LogInformation(
            "Notice {NoticeId} published by admin {AdminId}, notified {Count} users",
            request.NoticeId, _currentUserService.UserId, targetUserIds.Count);

        return new PublishNoticeResponse("Notice published successfully. It is now immutable.");
    }

    private async Task<List<Guid>> ResolveTargetUserIds(
        string targetAudience,
        IReadOnlyCollection<Guid> targetClassIds,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUserService.SchoolId;

        if (targetAudience == "All")
        {
            // Notify all active users in the school (teachers + parents)
            return await _context.Users
                .Where(u => u.SchoolId == schoolId && u.IsActive && u.Role != "Admin")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }

        if ((targetAudience == "Class" || targetAudience == "Section") && targetClassIds.Count > 0)
        {
            // Notify teachers assigned to the targeted class sections + parents of students in those sections.
            var teacherIds = await _context.TeacherClassAssignments
                .Where(tca => tca.SchoolId == schoolId && targetClassIds.Contains(tca.ClassId))
                .Select(tca => tca.TeacherId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var parentIds = await _context.ParentStudentLinks
                .Where(psl =>
                    psl.SchoolId == schoolId &&
                    psl.Student != null &&
                    targetClassIds.Contains(psl.Student.ClassId) &&
                    psl.Student.IsActive)
                .Select(psl => psl.ParentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return teacherIds.Union(parentIds).Distinct().ToList();
        }

        return new List<Guid>();
    }
}
