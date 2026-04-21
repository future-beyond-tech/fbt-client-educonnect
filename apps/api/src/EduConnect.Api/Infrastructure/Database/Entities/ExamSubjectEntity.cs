namespace EduConnect.Api.Infrastructure.Database.Entities;

/// <summary>
/// One scheduled paper inside an exam (e.g. "Math" on 2026-05-10 09:00).
/// Subject is stored as free text to match the existing pattern in
/// <see cref="TeacherClassAssignmentEntity"/> and <see cref="HomeworkEntity"/>
/// where subject is denormalised rather than FK'd to <see cref="SubjectEntity"/>.
/// </summary>
public class ExamSubjectEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ExamId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateOnly ExamDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal MaxMarks { get; set; }
    public string? Room { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ExamEntity? Exam { get; set; }
    public ICollection<ExamResultEntity> Results { get; set; } = [];
}
