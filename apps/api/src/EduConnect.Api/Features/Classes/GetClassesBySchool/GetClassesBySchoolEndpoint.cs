using MediatR;

namespace EduConnect.Api.Features.Classes.GetClassesBySchool;

public static class GetClassesBySchoolEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetClassesBySchoolQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
