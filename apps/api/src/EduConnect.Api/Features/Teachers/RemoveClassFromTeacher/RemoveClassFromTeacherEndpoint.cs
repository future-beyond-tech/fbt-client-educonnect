using MediatR;

namespace EduConnect.Api.Features.Teachers.RemoveClassFromTeacher;

public static class RemoveClassFromTeacherEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        Guid assignmentId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RemoveClassFromTeacherCommand(id, assignmentId),
            cancellationToken);
        return Results.Ok(result);
    }
}
