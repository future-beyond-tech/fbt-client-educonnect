namespace EduConnect.Api.Features.Notices.CreateNotice;

public record CreateNoticeCommand(
    string Title,
    string Body,
    string TargetAudience,
    List<Guid>? TargetClassIds = null,
    DateTimeOffset? ExpiresAt = null) : IRequest<CreateNoticeResponse>;

public record CreateNoticeResponse(Guid NoticeId, string Message);
