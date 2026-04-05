using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Notifications.MarkAllNotificationsRead;

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, MarkAllNotificationsReadResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public MarkAllNotificationsReadCommandHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MarkAllNotificationsReadResponse> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n =>
                n.UserId == _currentUserService.UserId &&
                n.SchoolId == _currentUserService.SchoolId &&
                !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return new MarkAllNotificationsReadResponse(0, "No unread notifications.");
        }

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new MarkAllNotificationsReadResponse(
            unreadNotifications.Count,
            $"{unreadNotifications.Count} notification(s) marked as read.");
    }
}
