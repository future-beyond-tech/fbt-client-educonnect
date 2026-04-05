using MediatR;

namespace EduConnect.Api.Features.Attendance.AdminOverride;

public static class AdminOverrideEndpoint
{
    public static async Task<IResult> Handle(
        Guid recordId,
        AdminOverrideCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { RecordId = recordId }, cancellationToken);
        return Results.Ok(result);
    }
}
