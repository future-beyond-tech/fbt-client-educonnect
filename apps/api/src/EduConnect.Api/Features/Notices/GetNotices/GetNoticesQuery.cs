namespace EduConnect.Api.Features.Notices.GetNotices;

public record GetNoticesQuery : IRequest<List<NoticeDto>>;

public record NoticeDto(
    Guid NoticeId,
    string Title,
    string Body,
    string TargetAudience,
    Guid? TargetClassId,
    bool IsPublished,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
