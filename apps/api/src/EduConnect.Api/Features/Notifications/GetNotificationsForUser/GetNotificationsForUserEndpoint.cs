using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduConnect.Api.Features.Notifications.GetNotificationsForUser;

public static class GetNotificationsForUserEndpoint
{
    public static async Task<IResult> Handle(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize < 1 ? 20 : pageSize > 50 ? 50 : pageSize;

        var result = await mediator.Send(
            new GetNotificationsForUserQuery(effectivePage, effectivePageSize),
            cancellationToken);

        return Results.Ok(result);
    }
}
