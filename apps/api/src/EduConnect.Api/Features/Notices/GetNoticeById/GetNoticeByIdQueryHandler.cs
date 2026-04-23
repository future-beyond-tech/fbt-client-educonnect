using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Notices.GetNotices;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Notices.GetNoticeById;

public class GetNoticeByIdQueryHandler : IRequestHandler<GetNoticeByIdQuery, NoticeDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetNoticeByIdQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<NoticeDto> Handle(GetNoticeByIdQuery request, CancellationToken cancellationToken)
    {
        var notice = await _context.Notices
            .Include(n => n.TargetClasses)
                .ThenInclude(tc => tc.TargetClass)
            .FirstOrDefaultAsync(n =>
                n.Id == request.NoticeId &&
                n.SchoolId == _currentUserService.SchoolId &&
                !n.IsDeleted,
                cancellationToken);

        if (notice == null)
        {
            throw new NotFoundException("Notice", request.NoticeId.ToString());
        }

        if (_currentUserService.Role == "Parent")
        {
            // Parents can only see a notice if it's published, not expired,
            // and either whole-school or targets one of their children's
            // classes. Anything else is treated as not-found to avoid
            // leaking draft existence.
            if (!notice.IsPublished ||
                (notice.ExpiresAt != null && notice.ExpiresAt <= DateTimeOffset.UtcNow))
            {
                throw new NotFoundException("Notice", request.NoticeId.ToString());
            }

            if (notice.TargetAudience != "All")
            {
                var studentClassIds = await _context.ParentStudentLinks
                    .Where(psl =>
                        psl.SchoolId == _currentUserService.SchoolId &&
                        psl.ParentId == _currentUserService.UserId)
                    .Join(_context.Students, psl => psl.StudentId, s => s.Id, (psl, s) => s.ClassId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var targets = notice.TargetClasses.Select(tc => tc.ClassId).ToList();
                if (!targets.Any(studentClassIds.Contains))
                {
                    throw new NotFoundException("Notice", request.NoticeId.ToString());
                }
            }
        }

        var targetClasses = notice.TargetClasses
            .OrderBy(targetClass => targetClass.TargetClass!.Name)
            .ThenBy(targetClass => targetClass.TargetClass!.AcademicYear)
            .ThenBy(targetClass => targetClass.TargetClass!.Section)
            .Select(targetClass => new NoticeTargetClassDto(
                targetClass.ClassId,
                targetClass.TargetClass!.Name,
                targetClass.TargetClass!.Section,
                targetClass.TargetClass!.AcademicYear))
            .ToList();

        return new NoticeDto(
            notice.Id,
            notice.Title,
            notice.Body,
            notice.TargetAudience,
            targetClasses,
            notice.IsPublished,
            notice.PublishedAt,
            notice.ExpiresAt,
            notice.CreatedAt,
            NoticeCapabilities.For(_currentUserService, notice.IsPublished, notice.PublishedById));
    }
}
