using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public static class GetAttachmentsForEntityEndpoint
{
    public static async Task<IResult> Handle(
        [FromQuery] Guid entityId,
        [FromQuery] string entityType,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetAttachmentsForEntityQuery(entityId, entityType),
            cancellationToken);

        return Results.Ok(result);
    }
}
