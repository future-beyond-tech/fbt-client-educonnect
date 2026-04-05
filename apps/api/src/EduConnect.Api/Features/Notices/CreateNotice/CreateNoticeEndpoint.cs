using MediatR;

namespace EduConnect.Api.Features.Notices.CreateNotice;

public static class CreateNoticeEndpoint
{
    public static async Task<IResult> Handle(
        CreateNoticeCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/notices/{result.NoticeId}", result);
    }
}
