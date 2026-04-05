namespace EduConnect.Api.Infrastructure.Database.Entities;

public class NoticeEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = string.Empty;
    public Guid? TargetClassId { get; set; }
    public Guid PublishedById { get; set; }
    public bool IsPublished { get; set; } = false;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ClassEntity? TargetClass { get; set; }
    public UserEntity? PublishedBy { get; set; }
}
