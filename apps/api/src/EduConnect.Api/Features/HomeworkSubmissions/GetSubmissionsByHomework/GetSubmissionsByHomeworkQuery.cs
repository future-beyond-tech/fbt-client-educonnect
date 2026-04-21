using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GetSubmissionsByHomework;

public record GetSubmissionsByHomeworkQuery(Guid HomeworkId)
    : IRequest<IReadOnlyList<HomeworkSubmissionListItem>>;

public record HomeworkSubmissionListItem(
    Guid SubmissionId,
    Guid StudentId,
    string StudentName,
    string? RollNumber,
    string Status,
    string? BodyText,
    string? Grade,
    string? Feedback,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? GradedAt);
