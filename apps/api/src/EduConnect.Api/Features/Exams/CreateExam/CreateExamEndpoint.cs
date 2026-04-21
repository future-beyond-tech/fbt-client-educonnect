using MediatR;

namespace EduConnect.Api.Features.Exams.CreateExam;

public static class CreateExamEndpoint
{
    public static async Task<IResult> Handle(
        CreateExamCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/exams/{result.ExamId}", result);
    }
}
