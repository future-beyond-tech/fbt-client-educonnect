using MediatR;

namespace EduConnect.Api.Features.Students.EnrollStudent;

public static class EnrollStudentEndpoint
{
    public static async Task<IResult> Handle(
        EnrollStudentCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/students/{result.StudentId}", result);
    }
}
