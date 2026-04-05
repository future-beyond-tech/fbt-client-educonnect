using MediatR;

namespace EduConnect.Api.Features.Notifications.MarkAllNotificationsRead;

public static class MarkAllNotificationsReadEndpoint
{
    public static async Task<IResult> Handle(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
        return Results.Ok(result);
    }
}
