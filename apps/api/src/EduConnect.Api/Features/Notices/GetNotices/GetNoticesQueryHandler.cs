using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Notices.GetNotices;

public class GetNoticesQueryHandler : IRequestHandler<GetNoticesQuery, List<NoticeDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetNoticesQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<NoticeDto>> Handle(GetNoticesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notices
            .Where(n => n.SchoolId == _currentUserService.SchoolId && !n.IsDeleted)
            .AsQueryable();

        if (_currentUserService.Role == "Parent")
        {
            var studentClassIds = await _context.ParentStudentLinks
                .Where(psl =>
                    psl.SchoolId == _currentUserService.SchoolId &&
                    psl.ParentId == _currentUserService.UserId)
                .Join(_context.Students, psl => psl.StudentId, s => s.Id, (psl, s) => s.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(n =>
                n.IsPublished &&
                (n.TargetAudience == "All" ||
                ((n.TargetAudience == "Class" || n.TargetAudience == "Section") &&
                 n.TargetClasses.Any(targetClass => studentClassIds.Contains(targetClass.ClassId)))))
                .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTimeOffset.UtcNow);
        }

        var rows = await query
            .OrderByDescending(n => n.PublishedAt ?? n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Body,
                n.TargetAudience,
                TargetClasses = n.TargetClasses
                    .OrderBy(targetClass => targetClass.TargetClass!.Name)
                    .ThenBy(targetClass => targetClass.TargetClass!.AcademicYear)
                    .ThenBy(targetClass => targetClass.TargetClass!.Section)
                    .Select(targetClass => new NoticeTargetClassDto(
                        targetClass.ClassId,
                        targetClass.TargetClass!.Name,
                        targetClass.TargetClass!.Section,
                        targetClass.TargetClass!.AcademicYear))
                    .ToList(),
                n.IsPublished,
                n.PublishedAt,
                n.ExpiresAt,
                n.CreatedAt,
                n.PublishedById
            })
            .ToListAsync(cancellationToken);

        // One grouped count query over all notice attachments the caller
        // can see, keyed by notice id. Mirrors the visibility filter in
        // GetAttachmentsForEntityQueryHandler so the badge on the list
        // matches what AttachmentList will render once the card expands.
        var noticeIds = rows.Select(r => r.Id).ToList();
        var countsByNoticeId = await LoadAttachmentCountsAsync(noticeIds, cancellationToken);

        return rows
            .Select(n => new NoticeDto(
                n.Id,
                n.Title,
                n.Body,
                n.TargetAudience,
                n.TargetClasses,
                n.IsPublished,
                n.PublishedAt,
                n.ExpiresAt,
                n.CreatedAt,
                NoticeCapabilities.For(_currentUserService, n.IsPublished, n.PublishedById),
                countsByNoticeId.GetValueOrDefault(n.Id, 0)))
            .ToList();
    }

    private async Task<Dictionary<Guid, int>> LoadAttachmentCountsAsync(
        IReadOnlyCollection<Guid> noticeIds,
        CancellationToken cancellationToken)
    {
        if (noticeIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // Admin/Teacher see Available + Pending + ScanFailed (so "scanning…"
        // and "blocked" badges line up with the attachment list view).
        // Parents / everyone else only see Available, matching the role
        // filter on GetAttachmentsForEntityQueryHandler.
        var visibleStatuses = _currentUserService.Role == "Admin" ||
                              _currentUserService.Role == "Teacher"
            ? new[]
            {
                AttachmentStatus.Available,
                AttachmentStatus.Pending,
                AttachmentStatus.ScanFailed,
            }
            : new[] { AttachmentStatus.Available };

        var grouped = await _context.Attachments
            .Where(a =>
                a.SchoolId == _currentUserService.SchoolId &&
                a.EntityType == "notice" &&
                a.EntityId != null &&
                noticeIds.Contains(a.EntityId.Value) &&
                visibleStatuses.Contains(a.Status))
            .GroupBy(a => a.EntityId!.Value)
            .Select(g => new { EntityId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(g => g.EntityId, g => g.Count);
    }
}
