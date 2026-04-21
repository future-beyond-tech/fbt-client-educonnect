using MediatR;

namespace EduConnect.Api.Features.Teachers.GetTeacherFilterMetadata;

public static class GetTeacherFilterMetadataEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTeacherFilterMetadataQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
