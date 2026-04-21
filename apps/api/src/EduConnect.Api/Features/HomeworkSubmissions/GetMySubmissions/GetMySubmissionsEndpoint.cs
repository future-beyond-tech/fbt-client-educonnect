using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GetMySubmissions;

public static class GetMySubmissionsEndpoint
{
    public static async Task<IResult> Handle(
        Guid? studentId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetMySubmissionsQuery(studentId),
            cancellationToken);
        return Results.Ok(result);
    }
}
