using MediatR;

namespace EduConnect.Api.Features.Exams.GetExamResults;

/// <summary>
/// Class-level results grid, used by the class teacher to enter/review marks
/// and by the UI to render the manual-upsert grid. Returns one row per
/// student and a column list per exam subject.
/// </summary>
public record GetExamResultsForClassQuery(Guid ExamId) : IRequest<ExamResultsGridDto>;

/// <summary>
/// Single-student view, used by parents. Returns the student's per-subject
/// line items + totals. Only accessible when results are finalized or when
/// the viewer is the class teacher.
/// </summary>
public record GetExamResultsForStudentQuery(Guid ExamId, Guid StudentId)
    : IRequest<ExamResultStudentDto>;

public record ExamResultsGridDto(
    Guid ExamId,
    string ExamName,
    string ClassName,
    string Section,
    bool IsResultsFinalized,
    DateTimeOffset? ResultsFinalizedAt,
    bool CanEditResults,
    List<ExamResultsSubjectColumn> Subjects,
    List<ExamResultsStudentRow> Students);

public record ExamResultsSubjectColumn(
    Guid ExamSubjectId,
    string Subject,
    decimal MaxMarks);

public record ExamResultsStudentRow(
    Guid StudentId,
    string RollNumber,
    string Name,
    List<ExamResultsCell> Cells);

public record ExamResultsCell(
    Guid ExamSubjectId,
    decimal? MarksObtained,
    string? Grade,
    string? Remarks,
    bool IsAbsent);

public record ExamResultStudentDto(
    Guid ExamId,
    string ExamName,
    string ClassName,
    string Section,
    string StudentName,
    string RollNumber,
    bool IsResultsFinalized,
    DateTimeOffset? ResultsFinalizedAt,
    decimal TotalObtained,
    decimal TotalMax,
    double Percentage,
    List<ExamResultStudentLine> Lines);

public record ExamResultStudentLine(
    Guid ExamSubjectId,
    string Subject,
    decimal MaxMarks,
    decimal? MarksObtained,
    string? Grade,
    string? Remarks,
    bool IsAbsent);
