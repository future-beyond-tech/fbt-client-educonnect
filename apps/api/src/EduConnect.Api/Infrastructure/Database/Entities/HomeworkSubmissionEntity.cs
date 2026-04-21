namespace EduConnect.Api.Infrastructure.Database.Entities;

public class HomeworkSubmissionEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid HomeworkId { get; set; }
    public Guid StudentId { get; set; }

    public string Status { get; set; } = HomeworkSubmissionStatus.Submitted;
    public string? BodyText { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Grade { get; set; }
    public string? Feedback { get; set; }
    public Guid? GradedById { get; set; }
    public DateTimeOffset? GradedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public HomeworkEntity? Homework { get; set; }
    public StudentEntity? Student { get; set; }
    public UserEntity? GradedBy { get; set; }
}

public static class HomeworkSubmissionStatus
{
    // Student's initial post. May become Late at save time if past due date.
    public const string Submitted = "Submitted";
    public const string Late = "Late";

    // Teacher has scored / given feedback. Still visible to student.
    public const string Graded = "Graded";

    // Teacher asked for a redo; student can re-submit which moves it back
    // to Submitted/Late. Not fully implemented in this phase (grade-only
    // ships first); constant reserved so migrations don't need redoing.
    public const string Returned = "Returned";

    public static readonly string[] All = { Submitted, Late, Graded, Returned };
}
