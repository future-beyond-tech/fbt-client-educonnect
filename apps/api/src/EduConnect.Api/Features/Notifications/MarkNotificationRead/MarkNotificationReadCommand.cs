namespace EduConnect.Api.Features.Notifications.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<MarkNotificationReadResponse>;

public record MarkNotificationReadResponse(string Message);
