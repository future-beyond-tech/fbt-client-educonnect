using MediatR;

namespace EduConnect.Api.Features.Homework.UpdateHomework;

public static class UpdateHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpdateHomeworkCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { HomeworkId = id }, cancellationToken);
        return Results.Ok(result);
    }
}
