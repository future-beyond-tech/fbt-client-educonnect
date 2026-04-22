using MediatR;

namespace EduConnect.Api.Features.Attachments.UploadAttachmentContent;

public record UploadAttachmentContentCommand(
    Guid AttachmentId,
    long SizeBytes,
    Stream Content) : IRequest<UploadAttachmentContentResponse>;

public record UploadAttachmentContentResponse(string Message);
