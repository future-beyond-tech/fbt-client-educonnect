using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GradeHomeworkSubmission;

public record GradeHomeworkSubmissionCommand(
    Guid SubmissionId,
    string Grade,
    string? Feedback) : IRequest<GradeHomeworkSubmissionResponse>;

public record GradeHomeworkSubmissionResponse(
    Guid SubmissionId,
    string Grade,
    string? Feedback,
    DateTimeOffset GradedAt);
