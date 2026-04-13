using MediatR;

namespace EduConnect.Api.Features.Homework.SubmitHomeworkForApproval;

public static class SubmitHomeworkForApprovalEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitHomeworkForApprovalCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}

