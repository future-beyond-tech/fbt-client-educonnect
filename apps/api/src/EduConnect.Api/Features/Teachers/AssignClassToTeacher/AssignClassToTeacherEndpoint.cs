using MediatR;

namespace EduConnect.Api.Features.Teachers.AssignClassToTeacher;

public static class AssignClassToTeacherEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        AssignClassToTeacherCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var commandWithTeacherId = command with { TeacherId = id };
        var result = await mediator.Send(commandWithTeacherId, cancellationToken);
        return Results.Created($"/api/teachers/{id}", result);
    }
}
