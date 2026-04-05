namespace EduConnect.Api.Features.Students.SearchParentsByPhone;

public record SearchParentsByPhoneQuery(string Phone) : IRequest<List<ParentSearchResultDto>>;

public record ParentSearchResultDto(
    Guid Id,
    string Name,
    string Phone);
