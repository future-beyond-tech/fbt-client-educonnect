using MediatR;

namespace EduConnect.Api.Features.Attachments.UploadAttachmentContent;

public static class UploadAttachmentContentEndpoint
{
    public static async Task<IResult> Handle(
        Guid attachmentId,
        HttpRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Request must be multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "A non-empty 'file' is required." });
        }

        await using var stream = file.OpenReadStream();
        var result = await mediator.Send(
            new UploadAttachmentContentCommand(attachmentId, file.Length, stream),
            cancellationToken);

        return Results.Ok(result);
    }
}
