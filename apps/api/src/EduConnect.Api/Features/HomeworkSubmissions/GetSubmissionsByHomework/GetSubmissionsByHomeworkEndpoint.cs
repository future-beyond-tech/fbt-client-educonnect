using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GetSubmissionsByHomework;

public static class GetSubmissionsByHomeworkEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetSubmissionsByHomeworkQuery(id),
            cancellationToken);
        return Results.Ok(result);
    }
}
