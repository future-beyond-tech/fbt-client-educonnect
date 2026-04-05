namespace EduConnect.Api.Infrastructure.Database.Entities;

public class ClassEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ICollection<StudentEntity> Students { get; set; } = [];
    public ICollection<TeacherClassAssignmentEntity> TeacherClassAssignments { get; set; } = [];
    public ICollection<HomeworkEntity> Homeworks { get; set; } = [];
    public ICollection<NoticeEntity> Notices { get; set; } = [];
}
