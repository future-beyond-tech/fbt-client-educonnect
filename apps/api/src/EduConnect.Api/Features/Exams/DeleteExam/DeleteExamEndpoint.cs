using MediatR;

namespace EduConnect.Api.Features.Exams.DeleteExam;

public static class DeleteExamEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteExamCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
