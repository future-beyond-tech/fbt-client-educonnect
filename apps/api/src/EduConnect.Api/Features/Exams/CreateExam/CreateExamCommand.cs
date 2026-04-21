using MediatR;

namespace EduConnect.Api.Features.Exams.CreateExam;

public record CreateExamSubjectInput(
    string Subject,
    DateOnly ExamDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal MaxMarks,
    string? Room);

public record CreateExamCommand(
    Guid ClassId,
    string Name,
    string AcademicYear,
    IReadOnlyList<CreateExamSubjectInput> Subjects) : IRequest<CreateExamResponse>;

public record CreateExamResponse(Guid ExamId, string Message);
