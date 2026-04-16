namespace EduConnect.Api.Infrastructure.Database.Entities;

public class SchoolEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserEntity> Users { get; set; } = [];
    public ICollection<ClassEntity> Classes { get; set; } = [];
    public ICollection<StudentEntity> Students { get; set; } = [];
    public ICollection<TeacherClassAssignmentEntity> TeacherClassAssignments { get; set; } = [];
    public ICollection<ParentStudentLinkEntity> ParentStudentLinks { get; set; } = [];
    public ICollection<AttendanceRecordEntity> AttendanceRecords { get; set; } = [];
    public ICollection<HomeworkEntity> Homeworks { get; set; } = [];
    public ICollection<NoticeEntity> Notices { get; set; } = [];
    public ICollection<NoticeTargetClassEntity> NoticeTargetClasses { get; set; } = [];
}
