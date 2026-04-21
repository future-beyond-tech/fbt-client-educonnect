using MediatR;

namespace EduConnect.Api.Features.Exams.GetExamById;

public static class GetExamByIdEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExamByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}
