namespace EduConnect.Api.Infrastructure.Database.Entities;

public class AttachmentEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public Guid UploadedById { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public UserEntity? UploadedBy { get; set; }
}
