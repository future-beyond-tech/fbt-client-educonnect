using MediatR;

namespace EduConnect.Api.Features.Students.DeactivateStudent;

public static class DeactivateStudentEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeactivateStudentCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
