using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.SubmitHomework;

public record SubmitHomeworkCommand(
    Guid HomeworkId,
    Guid StudentId,
    string? BodyText,
    IReadOnlyList<Guid>? AttachmentIds) : IRequest<SubmitHomeworkResponse>;

public record SubmitHomeworkResponse(
    Guid SubmissionId,
    string Status,
    DateTimeOffset SubmittedAt);
