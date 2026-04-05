using MediatR;

namespace EduConnect.Api.Features.Notifications.GetUnreadCount;

public static class GetUnreadCountEndpoint
{
    public static async Task<IResult> Handle(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUnreadCountQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
