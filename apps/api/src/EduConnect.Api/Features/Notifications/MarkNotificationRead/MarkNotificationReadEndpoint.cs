using MediatR;

namespace EduConnect.Api.Features.Notifications.MarkNotificationRead;

public static class MarkNotificationReadEndpoint
{
    public static async Task<IResult> Handle(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkNotificationReadCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
