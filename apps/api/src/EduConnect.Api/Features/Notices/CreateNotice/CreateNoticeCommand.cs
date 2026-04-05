namespace EduConnect.Api.Features.Notices.CreateNotice;

public record CreateNoticeCommand(
    string Title,
    string Body,
    string TargetAudience,
    Guid? TargetClassId = null,
    DateTimeOffset? ExpiresAt = null) : IRequest<CreateNoticeResponse>;

public record CreateNoticeResponse(Guid NoticeId, string Message);
