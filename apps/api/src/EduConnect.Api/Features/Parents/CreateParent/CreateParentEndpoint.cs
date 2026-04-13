using MediatR;

namespace EduConnect.Api.Features.Parents.CreateParent;

public static class CreateParentEndpoint
{
    public static async Task<IResult> Handle(
        CreateParentCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/parents/{result.ParentId}", result);
    }
}
