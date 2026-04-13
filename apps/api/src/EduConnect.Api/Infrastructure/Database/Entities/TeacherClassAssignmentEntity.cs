namespace EduConnect.Api.Infrastructure.Database.Entities;

public class TeacherClassAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid ClassId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public bool IsClassTeacher { get; set; } = false;
    public Guid? AssignedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public UserEntity? Teacher { get; set; }
    public ClassEntity? Class { get; set; }
    public UserEntity? AssignedBy { get; set; }
}
