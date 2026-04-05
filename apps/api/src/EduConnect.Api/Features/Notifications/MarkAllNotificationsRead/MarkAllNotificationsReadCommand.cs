namespace EduConnect.Api.Features.Notifications.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand() : IRequest<MarkAllNotificationsReadResponse>;

public record MarkAllNotificationsReadResponse(int MarkedCount, string Message);
