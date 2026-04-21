using MediatR;

namespace EduConnect.Api.Features.Exams.UpdateExam;

public static class UpdateExamEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpdateExamRequestBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateExamCommand(id, body.Name, body.AcademicYear, body.Subjects);
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

/// <summary>
/// Route payload for PUT /api/exams/{id}. The ExamId is taken from the route
/// so the client body doesn't carry it.
/// </summary>
public record UpdateExamRequestBody(
    string Name,
    string AcademicYear,
    IReadOnlyList<EduConnect.Api.Features.Exams.CreateExam.CreateExamSubjectInput> Subjects);
