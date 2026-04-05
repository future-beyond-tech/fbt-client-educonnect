namespace EduConnect.Api.Features.Notifications.GetUnreadCount;

public record GetUnreadCountQuery() : IRequest<UnreadCountDto>;

public record UnreadCountDto(int Count);
