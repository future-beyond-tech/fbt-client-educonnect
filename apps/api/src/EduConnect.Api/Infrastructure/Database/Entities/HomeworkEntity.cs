namespace EduConnect.Api.Infrastructure.Database.Entities;

public class HomeworkEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ClassId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid AssignedById { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsEditable { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ClassEntity? Class { get; set; }
    public UserEntity? AssignedBy { get; set; }
}
