using MediatR;

namespace EduConnect.Api.Features.Teachers.PromoteClassTeacher;

public static class PromoteClassTeacherEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        Guid assignmentId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new PromoteClassTeacherCommand(id, assignmentId),
            cancellationToken);

        return Results.Ok(result);
    }
}
