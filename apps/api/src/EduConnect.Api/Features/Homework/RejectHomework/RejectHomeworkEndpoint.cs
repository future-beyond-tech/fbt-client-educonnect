using MediatR;

namespace EduConnect.Api.Features.Homework.RejectHomework;

public static class RejectHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        RejectHomeworkRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RejectHomeworkCommand(id, request.Reason), cancellationToken);
        return Results.Ok(result);
    }
}

public record RejectHomeworkRequest(string Reason);

