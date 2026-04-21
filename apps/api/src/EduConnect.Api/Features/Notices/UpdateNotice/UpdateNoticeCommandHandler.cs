using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Notices.UpdateNotice;

public class UpdateNoticeCommandHandler : IRequestHandler<UpdateNoticeCommand, UpdateNoticeResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpdateNoticeCommandHandler> _logger;

    public UpdateNoticeCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpdateNoticeCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpdateNoticeResponse> Handle(UpdateNoticeCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to update notice",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only administrators can update notices.");
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
            _logger.LogWarning(
                "Attempt to edit published notice {NoticeId}",
                request.NoticeId);
            throw new ForbiddenException("Published notices are immutable and cannot be edited.");
        }

        // Authorship guard: only the admin who drafted the notice may edit it.
        // Mirrors the Homework editing rule (creator-only).
        if (notice.PublishedById != _currentUserService.UserId)
        {
            _logger.LogWarning(
                "Admin {AdminId} attempted to edit draft notice authored by {AuthorId}",
                _currentUserService.UserId, notice.PublishedById);
            throw new ForbiddenException("You can only edit draft notices that you created.");
        }

        var targetClasses = await ResolveTargetClassesAsync(request, cancellationToken);

        notice.Title = request.Title.Trim();
        notice.Body = request.Body.Trim();
        notice.TargetAudience = request.TargetAudience;
        notice.ExpiresAt = request.ExpiresAt;
        notice.UpdatedAt = DateTimeOffset.UtcNow;

        // Replace target-class rows: drafts don't have any downstream side
        // effects yet, so a clean wipe-and-re-insert is the simplest semantics.
        if (notice.TargetClasses.Count > 0)
        {
            _context.NoticeTargetClasses.RemoveRange(notice.TargetClasses);
        }

        if (targetClasses.Count > 0)
        {
            _context.NoticeTargetClasses.AddRange(targetClasses.Select(targetClass => new NoticeTargetClassEntity
            {
                NoticeId = notice.Id,
                ClassId = targetClass.Id,
                SchoolId = _currentUserService.SchoolId,
                CreatedAt = DateTimeOffset.UtcNow
            }));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notice {NoticeId} draft updated by admin {AdminId}",
            notice.Id, _currentUserService.UserId);

        return new UpdateNoticeResponse(notice.Id, "Draft notice updated successfully.");
    }

    private async Task<List<ClassEntity>> ResolveTargetClassesAsync(
        UpdateNoticeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TargetAudience == "All")
        {
            return [];
        }

        var targetClassIds = request.TargetClassIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];

        if (targetClassIds.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "TargetClassIds", ["Select at least one class section for targeted notices."] }
            });
        }

        var targetClasses = await _context.Classes
            .Where(c =>
                c.SchoolId == _currentUserService.SchoolId &&
                targetClassIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (targetClasses.Count != targetClassIds.Count)
        {
            _logger.LogWarning(
                "One or more selected notice target classes were not found in school {SchoolId}",
                _currentUserService.SchoolId);
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "TargetClassIds", ["One or more selected class sections were not found."] }
            });
        }

        var classGroups = targetClasses
            .Select(c => (c.Name, c.AcademicYear))
            .Distinct()
            .ToList();

        if (classGroups.Count != 1)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "TargetClassIds", ["Select sections from a single class and academic year."] }
            });
        }

        if (request.TargetAudience == "Class")
        {
            var (className, academicYear) = classGroups[0];
            var expectedClassIds = await _context.Classes
                .Where(c =>
                    c.SchoolId == _currentUserService.SchoolId &&
                    c.Name == className &&
                    c.AcademicYear == academicYear)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var missingSections = expectedClassIds.Except(targetClassIds).ToList();
            if (missingSections.Count > 0)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "TargetClassIds", ["Class-wide notices must include all sections for the selected class."] }
                });
            }
        }

        return targetClasses;
    }
}
