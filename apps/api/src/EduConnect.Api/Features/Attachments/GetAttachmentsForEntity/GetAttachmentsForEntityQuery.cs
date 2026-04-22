namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public record GetAttachmentsForEntityQuery(Guid EntityId, string EntityType) : IRequest<List<AttachmentDto>>;

/// <summary>
/// <see cref="DownloadUrl"/> is non-null only when <see cref="Status"/> is
/// <c>Available</c>. Pending / ScanFailed rows are exposed to admins +
/// teachers (so they can see "scanning…" / "blocked" badges) but the
/// presigned URL is withheld until the scan clears — the read path can
/// never hand out an unscanned object.
/// </summary>
public record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    int SizeBytes,
    string? DownloadUrl,
    DateTimeOffset UploadedAt,
    string Status);
