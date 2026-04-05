namespace EduConnect.Api.Infrastructure.Database.Entities;

public class AttendanceRecordEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid EnteredById { get; set; }
    public string EnteredByRole { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public StudentEntity? Student { get; set; }
    public UserEntity? EnteredBy { get; set; }
}
