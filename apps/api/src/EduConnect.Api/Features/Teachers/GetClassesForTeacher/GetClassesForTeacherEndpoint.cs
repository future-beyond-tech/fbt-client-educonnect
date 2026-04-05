using MediatR;

namespace EduConnect.Api.Features.Teachers.GetClassesForTeacher;

public static class GetClassesForTeacherEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetClassesForTeacherQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
