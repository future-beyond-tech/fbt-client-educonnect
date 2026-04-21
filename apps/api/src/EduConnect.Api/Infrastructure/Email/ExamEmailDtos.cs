namespace EduConnect.Api.Infrastructure.Email;

/// <summary>
/// One row in the exam schedule email's timetable. Captures everything a
/// parent needs to know per subject paper: when, where, duration, and
/// maximum marks.
/// </summary>
public sealed record ExamScheduleSubjectLine(
    string Subject,
    DateOnly ExamDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal MaxMarks,
    string? Room);

/// <summary>
/// One row in the exam result email. MarksObtained is nullable because a
/// paper can be graded by letter alone, or the student can have been absent.
/// </summary>
public sealed record ExamResultSubjectLine(
    string Subject,
    decimal? MarksObtained,
    decimal MaxMarks,
    string? Grade,
    bool IsAbsent);
