using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Notifications.GetUnreadCount;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, UnreadCountDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetUnreadCountQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UnreadCountDto> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var count = await _context.Notifications
            .CountAsync(n =>
                n.UserId == _currentUserService.UserId &&
                n.SchoolId == _currentUserService.SchoolId &&
                !n.IsRead,
                cancellationToken);

        return new UnreadCountDto(count);
    }
}
