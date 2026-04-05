using MediatR;

namespace EduConnect.Api.Features.Subjects.CreateSubject;

public static class CreateSubjectEndpoint
{
    public static async Task<IResult> Handle(
        CreateSubjectCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/subjects/{result.SubjectId}", result);
    }
}
