using MediatR;

namespace EduConnect.Api.Features.Attendance.RejectLeave;

public static class RejectLeaveEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        RejectLeaveCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { LeaveApplicationId = id }, cancellationToken);
        return Results.Ok(result);
    }
}

