using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Models;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Notifications.GetNotificationsForUser;

public class GetNotificationsForUserQueryHandler : IRequestHandler<GetNotificationsForUserQuery, PagedResult<NotificationDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetNotificationsForUserQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<NotificationDto>> Handle(GetNotificationsForUserQuery request, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);

        var query = _context.Notifications
            .Where(n => n.UserId == _currentUserService.UserId
                && n.SchoolId == _currentUserService.SchoolId
                && n.CreatedAt >= cutoff)
            .OrderByDescending(n => n.IsRead == false ? 1 : 0)
            .ThenByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Title,
                n.Body,
                n.EntityId,
                n.EntityType,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<NotificationDto>.Create(items, totalCount, request.Page, request.PageSize);
    }
}
