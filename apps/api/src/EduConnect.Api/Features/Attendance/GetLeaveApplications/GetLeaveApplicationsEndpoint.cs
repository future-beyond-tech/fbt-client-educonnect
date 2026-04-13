using MediatR;

namespace EduConnect.Api.Features.Attendance.GetLeaveApplications;

public static class GetLeaveApplicationsEndpoint
{
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken,
        Guid? studentId = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = new GetLeaveApplicationsQuery(studentId, status, pageNumber, pageSize);
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
