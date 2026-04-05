namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public record GetAttachmentsForEntityQuery(Guid EntityId, string EntityType) : IRequest<List<AttachmentDto>>;

public record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    int SizeBytes,
    string DownloadUrl,
    DateTimeOffset UploadedAt);
