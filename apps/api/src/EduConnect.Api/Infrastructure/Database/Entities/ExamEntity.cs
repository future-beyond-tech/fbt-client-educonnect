namespace EduConnect.Api.Infrastructure.Database.Entities;

public class ExamEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ClassId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }

    // Schedule publish lifecycle. A schedule becomes notifiable only once
    // SchedulePublishedAt is set — mirrors Notice.IsPublished / PublishedAt.
    public bool IsSchedulePublished { get; set; } = false;
    public DateTimeOffset? SchedulePublishedAt { get; set; }

    // Results are finalised separately: a class teacher reviews, locks the
    // grid, and then sends result notifications. Once finalised the
    // per-subject ExamResult rows become immutable.
    public bool IsResultsFinalized { get; set; } = false;
    public DateTimeOffset? ResultsFinalizedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ClassEntity? Class { get; set; }
    public UserEntity? CreatedBy { get; set; }
    public ICollection<ExamSubjectEntity> Subjects { get; set; } = [];
    public ICollection<ExamResultEntity> Results { get; set; } = [];
}
