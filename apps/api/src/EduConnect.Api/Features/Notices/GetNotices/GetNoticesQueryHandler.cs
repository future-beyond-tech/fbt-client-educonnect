using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
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
                NoticeCapabilities.For(_currentUserService, n.IsPublished, n.PublishedById)))
            .ToList();
    }
}
