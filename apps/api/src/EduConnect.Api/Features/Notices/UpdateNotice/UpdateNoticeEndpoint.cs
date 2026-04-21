using MediatR;

namespace EduConnect.Api.Features.Notices.UpdateNotice;

public static class UpdateNoticeEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpdateNoticeRequest body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateNoticeCommand(
            id,
            body.Title,
            body.Body,
            body.TargetAudience,
            body.TargetClassIds,
            body.ExpiresAt);

        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

// Route binds {id} from the path, so the request body only carries the fields.
public record UpdateNoticeRequest(
    string Title,
    string Body,
    string TargetAudience,
    List<Guid>? TargetClassIds,
    DateTimeOffset? ExpiresAt);
