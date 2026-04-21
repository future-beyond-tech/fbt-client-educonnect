using MediatR;

namespace EduConnect.Api.Features.Exams.FinalizeExamResults;

public static class FinalizeExamResultsEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new FinalizeExamResultsCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
