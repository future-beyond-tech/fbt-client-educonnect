using MediatR;

namespace EduConnect.Api.Features.Homework.ApproveHomework;

public static class ApproveHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApproveHomeworkCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}

