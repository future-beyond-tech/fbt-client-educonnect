using EduConnect.Api.Common.Models;
using MediatR;

namespace EduConnect.Api.Features.Students.GetStudentsByClass;

public static class GetStudentsByClassEndpoint
{
    public static async Task<IResult> Handle(
        [AsParameters] GetStudentsByClassQuery query,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
