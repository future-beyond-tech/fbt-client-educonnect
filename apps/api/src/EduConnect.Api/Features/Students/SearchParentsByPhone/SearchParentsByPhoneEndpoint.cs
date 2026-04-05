using MediatR;

namespace EduConnect.Api.Features.Students.SearchParentsByPhone;

public static class SearchParentsByPhoneEndpoint
{
    public static async Task<IResult> Handle(
        [AsParameters] SearchParentsByPhoneQuery query,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(result);
    }
}
