using MediatR;

namespace EduConnect.Api.Features.Teachers.GetTeacherProfile;

public static class GetTeacherProfileEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTeacherProfileQuery(id), cancellationToken);
        return Results.Ok(result);
    }
}
