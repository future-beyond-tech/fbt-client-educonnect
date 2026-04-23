using MediatR;

namespace EduConnect.Api.Features.Notices.GetNoticeById;

public static class GetNoticeByIdEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNoticeByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}
