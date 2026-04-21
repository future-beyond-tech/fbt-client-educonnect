namespace EduConnect.Api.Infrastructure.Database.Entities;

/// <summary>
/// One student's mark in one subject of one exam. Grade is stored alongside
/// marks so schools that grade-only (no raw marks) or mark-only can both
/// render cleanly — whichever side is null is hidden in the parent view.
/// </summary>
public class ExamResultEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ExamId { get; set; }
    public Guid ExamSubjectId { get; set; }
    public Guid StudentId { get; set; }

    /// <summary>Raw marks obtained (0..MaxMarks). Null when the paper was
    /// absent/NE or when the school only records grades.</summary>
    public decimal? MarksObtained { get; set; }

    /// <summary>Letter grade (A+, A, B, etc.). Null when school only
    /// records raw marks.</summary>
    public string? Grade { get; set; }

    /// <summary>Teacher-entered comment / remark. Kept short to fit in
    /// the per-cell grid UI.</summary>
    public string? Remarks { get; set; }

    /// <summary>True when the student was absent for this paper. MarksObtained
    /// must be null when this is true (enforced in the validator).</summary>
    public bool IsAbsent { get; set; } = false;

    public Guid RecordedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ExamEntity? Exam { get; set; }
    public ExamSubjectEntity? ExamSubject { get; set; }
    public StudentEntity? Student { get; set; }
    public UserEntity? RecordedBy { get; set; }
}
