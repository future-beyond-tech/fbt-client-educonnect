namespace EduConnect.Api.Features.Notices.PublishNotice;

public record PublishNoticeCommand(Guid NoticeId) : IRequest<PublishNoticeResponse>;

public record PublishNoticeResponse(string Message);
