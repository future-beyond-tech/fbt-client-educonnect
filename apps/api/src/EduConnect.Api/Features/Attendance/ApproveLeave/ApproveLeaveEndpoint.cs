using MediatR;

namespace EduConnect.Api.Features.Attendance.ApproveLeave;

public static class ApproveLeaveEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApproveLeaveCommand(id), cancellationToken);
        return Results.Ok(result);
    }
}

