namespace EduConnect.Api.Features.Attachments.DeleteAttachment;

public record DeleteAttachmentCommand(Guid AttachmentId) : IRequest<DeleteAttachmentResponse>;

public record DeleteAttachmentResponse(string Message);
