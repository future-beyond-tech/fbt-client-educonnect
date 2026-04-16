using MediatR;

namespace EduConnect.Api.Features.Attendance.SubmitAttendanceTake;

public static class SubmitAttendanceTakeEndpoint
{
    public static async Task<IResult> Handle(
        SubmitAttendanceTakeCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}

