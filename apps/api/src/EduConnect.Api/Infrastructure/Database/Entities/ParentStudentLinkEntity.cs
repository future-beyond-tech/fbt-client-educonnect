namespace EduConnect.Api.Infrastructure.Database.Entities;

public class ParentStudentLinkEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ParentId { get; set; }
    public Guid StudentId { get; set; }
    public string Relationship { get; set; } = "parent";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public UserEntity? Parent { get; set; }
    public StudentEntity? Student { get; set; }
}
