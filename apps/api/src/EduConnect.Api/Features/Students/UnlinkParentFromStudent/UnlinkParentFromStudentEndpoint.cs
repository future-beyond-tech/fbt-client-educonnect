using MediatR;

namespace EduConnect.Api.Features.Students.UnlinkParentFromStudent;

public static class UnlinkParentFromStudentEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        Guid linkId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UnlinkParentFromStudentCommand(id, linkId),
            cancellationToken);
        return Results.Ok(result);
    }
}
