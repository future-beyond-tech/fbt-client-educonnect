using MediatR;

namespace EduConnect.Api.Features.Classes.CreateClass;

public static class CreateClassEndpoint
{
    public static async Task<IResult> Handle(
        CreateClassCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/classes/{result.ClassId}", result);
    }
}
