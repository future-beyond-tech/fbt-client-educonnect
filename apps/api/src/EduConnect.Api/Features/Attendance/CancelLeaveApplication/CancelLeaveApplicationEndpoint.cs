using MediatR;

namespace EduConnect.Api.Features.Attendance.CancelLeaveApplication;

public static class CancelLeaveApplicationEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelLeaveApplicationCommand(id), cancellationToken);
        return Results.NoContent();
    }
}

