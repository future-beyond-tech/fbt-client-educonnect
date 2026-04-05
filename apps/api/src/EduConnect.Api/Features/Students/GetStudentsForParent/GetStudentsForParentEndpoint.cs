using MediatR;

namespace EduConnect.Api.Features.Students.GetStudentsForParent;

public static class GetStudentsForParentEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentsForParentQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
