using MediatR;

namespace EduConnect.Api.Features.Exams.GetExamById;

public record GetExamByIdQuery(Guid ExamId) : IRequest<ExamDetailDto>;

public record ExamSubjectDto(
    Guid Id,
    string Subject,
    DateOnly ExamDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal MaxMarks,
    string? Room);

public record ExamDetailDto(
    Guid ExamId,
    Guid ClassId,
    string ClassName,
    string Section,
    string Name,
    string AcademicYear,
    bool IsSchedulePublished,
    DateTimeOffset? SchedulePublishedAt,
    bool IsResultsFinalized,
    DateTimeOffset? ResultsFinalizedAt,
    bool CanEditSchedule,
    bool CanEditResults,
    List<ExamSubjectDto> Subjects);
