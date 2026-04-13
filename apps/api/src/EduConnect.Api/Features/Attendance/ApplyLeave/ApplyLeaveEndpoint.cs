using MediatR;

namespace EduConnect.Api.Features.Attendance.ApplyLeave;

public static class ApplyLeaveEndpoint
{
    public static async Task<IResult> Handle(
        ApplyLeaveCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/attendance/leave/{result.LeaveApplicationId}", result);
    }
}
