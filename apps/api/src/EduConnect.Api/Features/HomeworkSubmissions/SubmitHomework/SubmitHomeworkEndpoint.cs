using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.SubmitHomework;

public static class SubmitHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        SubmitHomeworkRequest body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new SubmitHomeworkCommand(
            id,
            body.StudentId,
            body.BodyText,
            body.AttachmentIds);
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

public record SubmitHomeworkRequest(
    Guid StudentId,
    string? BodyText,
    IReadOnlyList<Guid>? AttachmentIds);
