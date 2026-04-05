namespace EduConnect.Api.Infrastructure.Database.Entities;

public class StudentEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ClassId { get; set; }
    public string RollNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ClassEntity? Class { get; set; }
    public UserEntity? CreatedBy { get; set; }
    public ICollection<ParentStudentLinkEntity> ParentLinks { get; set; } = [];
    public ICollection<AttendanceRecordEntity> AttendanceRecords { get; set; } = [];
}
