using MediatR;

namespace EduConnect.Api.Features.Attendance.UpdateLeaveApplication;

public static class UpdateLeaveApplicationEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        UpdateLeaveApplicationCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { LeaveApplicationId = id }, cancellationToken);
        return Results.Ok(result);
    }
}

