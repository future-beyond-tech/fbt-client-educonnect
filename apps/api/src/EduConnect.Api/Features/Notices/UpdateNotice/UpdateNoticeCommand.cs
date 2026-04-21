namespace EduConnect.Api.Features.Notices.UpdateNotice;

public record UpdateNoticeCommand(
    Guid NoticeId,
    string Title,
    string Body,
    string TargetAudience,
    List<Guid>? TargetClassIds = null,
    DateTimeOffset? ExpiresAt = null) : IRequest<UpdateNoticeResponse>;

public record UpdateNoticeResponse(Guid NoticeId, string Message);
