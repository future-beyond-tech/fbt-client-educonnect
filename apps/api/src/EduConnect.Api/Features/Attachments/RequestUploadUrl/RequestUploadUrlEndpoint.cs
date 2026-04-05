using MediatR;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrl;

public static class RequestUploadUrlEndpoint
{
    public static async Task<IResult> Handle(RequestUploadUrlCommand command, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}
