using MediatR;

namespace EduConnect.Api.Features.Notices.GetNotices;

public static class GetNoticesEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNoticesQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
