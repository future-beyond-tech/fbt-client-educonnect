using MediatR;

namespace EduConnect.Api.Features.Students.LinkParentToStudent;

public static class LinkParentToStudentEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        LinkParentToStudentCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var commandWithStudentId = command with { StudentId = id };
        var result = await mediator.Send(commandWithStudentId, cancellationToken);
        return Results.Created($"/api/students/{id}/parent-links/{result.LinkId}", result);
    }
}
