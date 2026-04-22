namespace EduConnect.Api.Features.Attachments.RequestUploadUrlV2;

public record RequestUploadUrlV2Command(
    string FileName,
    string ContentType,
    int SizeBytes,
    string EntityType) : IRequest<RequestUploadUrlV2Response>;

// storageKey intentionally omitted — clients PUT directly to uploadUrl
// and reference the attachment by AttachmentId when calling /attach.
// See apps/api/docs/attachment-virus-scanning.md.
public record RequestUploadUrlV2Response(
    string UploadUrl,
    Guid AttachmentId);
