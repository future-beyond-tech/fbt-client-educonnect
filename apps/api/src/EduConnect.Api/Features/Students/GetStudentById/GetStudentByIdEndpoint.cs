using MediatR;

namespace EduConnect.Api.Features.Students.GetStudentById;

public static class GetStudentByIdEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}
