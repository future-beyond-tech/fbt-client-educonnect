namespace EduConnect.Api.Features.Students.GetStudentsForParent;

public record GetStudentsForParentQuery() : IRequest<List<ParentChildDto>>;

public record ParentChildDto(
    Guid Id,
    string Name,
    string RollNumber,
    Guid ClassId,
    string ClassName,
    string Section,
    string Relationship,
    bool IsActive);
