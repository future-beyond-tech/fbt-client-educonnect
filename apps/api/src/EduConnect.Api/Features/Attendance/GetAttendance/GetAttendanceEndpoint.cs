using MediatR;

namespace EduConnect.Api.Features.Attendance.GetAttendance;

public static class GetAttendanceEndpoint
{
    public static async Task<IResult> Handle(
        [AsParameters] GetAttendanceQuery query,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
