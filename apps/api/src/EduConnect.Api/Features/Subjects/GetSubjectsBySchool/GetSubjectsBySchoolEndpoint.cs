using MediatR;

namespace EduConnect.Api.Features.Subjects.GetSubjectsBySchool;

public static class GetSubjectsBySchoolEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubjectsBySchoolQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
