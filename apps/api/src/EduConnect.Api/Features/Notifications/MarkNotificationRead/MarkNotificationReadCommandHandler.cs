using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Notifications.MarkNotificationRead;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, MarkNotificationReadResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public MarkNotificationReadCommandHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MarkNotificationReadResponse> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n =>
                n.Id == request.NotificationId &&
                n.UserId == _currentUserService.UserId &&
                n.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (notification == null)
        {
            throw new NotFoundException("Notification", request.NotificationId.ToString());
        }

        if (notification.IsRead)
        {
            return new MarkNotificationReadResponse("Notification already read.");
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync(cancellationToken);

        return new MarkNotificationReadResponse("Notification marked as read.");
    }
}
