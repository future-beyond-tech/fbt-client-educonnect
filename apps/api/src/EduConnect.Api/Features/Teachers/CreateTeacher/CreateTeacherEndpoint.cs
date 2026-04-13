using MediatR;

namespace EduConnect.Api.Features.Teachers.CreateTeacher;

public static class CreateTeacherEndpoint
{
    public static async Task<IResult> Handle(
        CreateTeacherCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/teachers/{result.TeacherId}", result);
    }
}
