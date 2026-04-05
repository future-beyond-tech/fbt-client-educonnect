using MediatR;

namespace EduConnect.Api.Features.Homework.GetHomework;

public static class GetHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        [AsParameters] GetHomeworkQuery query,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
