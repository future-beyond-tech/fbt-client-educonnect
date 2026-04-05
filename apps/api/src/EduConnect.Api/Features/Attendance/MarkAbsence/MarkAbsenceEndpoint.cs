using MediatR;

namespace EduConnect.Api.Features.Attendance.MarkAbsence;

public static class MarkAbsenceEndpoint
{
    public static async Task<IResult> Handle(
        MarkAbsenceCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/attendance/{result.RecordId}", result);
    }
}
