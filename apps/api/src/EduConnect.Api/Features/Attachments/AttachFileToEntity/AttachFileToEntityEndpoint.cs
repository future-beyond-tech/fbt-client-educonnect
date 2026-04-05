using MediatR;

namespace EduConnect.Api.Features.Attachments.AttachFileToEntity;

public static class AttachFileToEntityEndpoint
{
    public static async Task<IResult> Handle(AttachFileToEntityCommand command, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}
