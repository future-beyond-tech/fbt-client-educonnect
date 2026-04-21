using MediatR;

namespace EduConnect.Api.Features.Exams.PublishExamSchedule;

public static class PublishExamScheduleEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PublishExamScheduleCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}
