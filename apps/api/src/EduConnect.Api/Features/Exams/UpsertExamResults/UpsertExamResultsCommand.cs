using MediatR;

namespace EduConnect.Api.Features.Exams.UpsertExamResults;

/// <summary>
/// Bulk upsert of marks for a single exam. Class teacher supplies one row per
/// (student, exam subject) with either raw marks, a letter grade, or an
/// IsAbsent=true flag. Existing rows are updated; missing ones are inserted.
///
/// This is the manual-entry path. A matching CSV upload endpoint parses the
/// same shape from a spreadsheet.
/// </summary>
public record UpsertExamResultsCommand(
    Guid ExamId,
    IReadOnlyList<ExamResultRowInput> Rows) : IRequest<UpsertExamResultsResponse>;

public record ExamResultRowInput(
    Guid StudentId,
    Guid ExamSubjectId,
    decimal? MarksObtained,
    string? Grade,
    string? Remarks,
    bool IsAbsent);

public record UpsertExamResultsResponse(
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<string> Warnings);
