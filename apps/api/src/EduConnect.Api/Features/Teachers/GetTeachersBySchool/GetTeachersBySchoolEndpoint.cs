using MediatR;

namespace EduConnect.Api.Features.Teachers.GetTeachersBySchool;

public static class GetTeachersBySchoolEndpoint
{
    public static async Task<IResult> Handle(
        [AsParameters] GetTeachersBySchoolQuery query,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
