namespace EduConnect.Api.Features.Classes.GetClassesBySchool;

public record GetClassesBySchoolQuery() : IRequest<List<ClassDto>>;

public record ClassDto(
    Guid Id,
    string Name,
    string Section,
    string AcademicYear,
    int StudentCount);
