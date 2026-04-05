using MediatR;

namespace EduConnect.Api.Features.Students.UpdateStudent;

public static class UpdateStudentEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpdateStudentCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Ensure route ID matches command ID
        var commandWithId = command with { Id = id };
        var result = await mediator.Send(commandWithId, cancellationToken);
        return Results.Ok(result);
    }
}
