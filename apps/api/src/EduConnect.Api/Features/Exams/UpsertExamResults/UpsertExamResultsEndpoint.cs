using MediatR;

namespace EduConnect.Api.Features.Exams.UpsertExamResults;

public static class UpsertExamResultsEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpsertExamResultsRequestBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpsertExamResultsCommand(id, body.Rows);
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

public record UpsertExamResultsRequestBody(IReadOnlyList<ExamResultRowInput> Rows);
