using EduConnect.Api.Common.Models;

namespace EduConnect.Api.Features.Notifications.GetNotificationsForUser;

public record GetNotificationsForUserQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<NotificationDto>>;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    Guid? EntityId,
    string? EntityType,
    bool IsRead,
    DateTimeOffset CreatedAt);
