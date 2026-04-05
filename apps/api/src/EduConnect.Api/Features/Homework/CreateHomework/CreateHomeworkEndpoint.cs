using MediatR;

namespace EduConnect.Api.Features.Homework.CreateHomework;

public static class CreateHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        CreateHomeworkCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/homework/{result.HomeworkId}", result);
    }
}
