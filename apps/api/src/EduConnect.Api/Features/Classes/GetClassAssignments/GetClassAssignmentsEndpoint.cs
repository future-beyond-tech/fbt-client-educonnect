using MediatR;

namespace EduConnect.Api.Features.Classes.GetClassAssignments;

public static class GetClassAssignmentsEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetClassAssignmentsQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}

