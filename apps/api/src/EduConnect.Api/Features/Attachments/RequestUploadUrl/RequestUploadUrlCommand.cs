namespace EduConnect.Api.Features.Attachments.RequestUploadUrl;

public record RequestUploadUrlCommand(
    string FileName,
    string ContentType,
    int SizeBytes) : IRequest<RequestUploadUrlResponse>;

public record RequestUploadUrlResponse(
    string UploadUrl,
    string StorageKey,
    Guid AttachmentId);
