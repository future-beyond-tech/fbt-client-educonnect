using MediatR;

namespace EduConnect.Api.Features.Attachments.DeleteAttachment;

public static class DeleteAttachmentEndpoint
{
    public static async Task<IResult> Handle(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteAttachmentCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
