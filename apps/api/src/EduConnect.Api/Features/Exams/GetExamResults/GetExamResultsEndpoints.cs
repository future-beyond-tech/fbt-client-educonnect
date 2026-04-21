using MediatR;

namespace EduConnect.Api.Features.Exams.GetExamResults;

public static class GetExamResultsForClassEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExamResultsForClassQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}

public static class GetExamResultsForStudentEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        Guid studentId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetExamResultsForStudentQuery(id, studentId),
            cancellationToken);
        return Results.Ok(result);
    }
}
