using EduConnect.Api.Features.Notices.GetNotices;

namespace EduConnect.Api.Features.Notices.GetNoticeById;

public record GetNoticeByIdQuery(Guid NoticeId) : IRequest<NoticeDto>;
