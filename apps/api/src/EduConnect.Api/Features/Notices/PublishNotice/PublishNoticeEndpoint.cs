using MediatR;

namespace EduConnect.Api.Features.Notices.PublishNotice;

public static class PublishNoticeEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PublishNoticeCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
