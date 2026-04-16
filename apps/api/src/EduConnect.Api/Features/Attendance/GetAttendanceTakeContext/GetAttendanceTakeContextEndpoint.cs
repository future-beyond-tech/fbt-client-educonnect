using MediatR;

namespace EduConnect.Api.Features.Attendance.GetAttendanceTakeContext;

public static class GetAttendanceTakeContextEndpoint
{
    public static async Task<IResult> Handle(
        Guid classId,
        DateOnly date,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAttendanceTakeContextQuery(classId, date), cancellationToken);
        return Results.Ok(result);
    }
}

