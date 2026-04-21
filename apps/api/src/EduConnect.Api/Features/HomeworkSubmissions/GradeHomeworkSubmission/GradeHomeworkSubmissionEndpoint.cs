using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GradeHomeworkSubmission;

public static class GradeHomeworkSubmissionEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        GradeHomeworkSubmissionRequest body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GradeHomeworkSubmissionCommand(id, body.Grade, body.Feedback),
            cancellationToken);
        return Results.Ok(result);
    }
}

public record GradeHomeworkSubmissionRequest(string Grade, string? Feedback);
