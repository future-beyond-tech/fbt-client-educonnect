namespace EduConnect.Api.Features.Attachments.RequestUploadUrlV2;

public record RequestUploadUrlV2Command(
    string FileName,
    string ContentType,
    int SizeBytes,
    string EntityType) : IRequest<RequestUploadUrlV2Response>;

public record RequestUploadUrlV2Response(
    string UploadUrl,
    Guid AttachmentId);
