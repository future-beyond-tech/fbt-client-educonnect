using MediatR;

namespace EduConnect.Api.Features.Classes.UpdateClass;

public static class UpdateClassEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpdateClassCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var commandWithId = command with { Id = id };
        var result = await mediator.Send(commandWithId, cancellationToken);
        return Results.Ok(result);
    }
}
