using MediatR;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrlV2;

public static class RequestUploadUrlV2Endpoint
{
    public static async Task<IResult> Handle(RequestUploadUrlV2Command command, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}
