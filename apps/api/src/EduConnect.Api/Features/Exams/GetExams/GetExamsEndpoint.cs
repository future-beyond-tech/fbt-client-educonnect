using MediatR;

namespace EduConnect.Api.Features.Exams.GetExams;

public static class GetExamsEndpoint
{
    public static async Task<IResult> Handle(
        Guid? classId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExamsQuery(classId), cancellationToken);
        return Results.Ok(result);
    }
}
