using MediatR;

namespace EduConnect.Api.Features.Exams.GetExams;

public record GetExamsQuery(Guid? ClassId = null) : IRequest<List<ExamListItemDto>>;

public record ExamListItemDto(
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
    int SubjectCount,
    DateOnly? FirstExamDate,
    DateOnly? LastExamDate,
    DateTimeOffset CreatedAt);
