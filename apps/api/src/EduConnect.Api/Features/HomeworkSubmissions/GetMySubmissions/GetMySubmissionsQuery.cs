using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GetMySubmissions;

public record GetMySubmissionsQuery(Guid? StudentId)
    : IRequest<IReadOnlyList<MyHomeworkSubmissionItem>>;

public record MyHomeworkSubmissionItem(
    Guid SubmissionId,
    Guid HomeworkId,
    string HomeworkTitle,
    string HomeworkSubject,
    Guid StudentId,
    string StudentName,
    string Status,
    string? BodyText,
    string? Grade,
    string? Feedback,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? GradedAt);
