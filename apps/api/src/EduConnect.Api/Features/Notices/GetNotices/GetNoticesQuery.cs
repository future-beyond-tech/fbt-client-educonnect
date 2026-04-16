namespace EduConnect.Api.Features.Notices.GetNotices;

public record GetNoticesQuery : IRequest<List<NoticeDto>>;

public record NoticeTargetClassDto(
    Guid ClassId,
    string ClassName,
    string Section,
    string AcademicYear);

public record NoticeDto(
    Guid NoticeId,
    string Title,
    string Body,
    string TargetAudience,
    List<NoticeTargetClassDto> TargetClasses,
    bool IsPublished,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
