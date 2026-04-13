namespace EduConnect.Api.Features.Students.GetStudentById;

public record GetStudentByIdQuery(Guid Id) : IRequest<StudentDetailDto>;

public record StudentDetailDto(
    Guid Id,
    string Name,
    string RollNumber,
    Guid ClassId,
    string ClassName,
    string Section,
    string AcademicYear,
    DateOnly? DateOfBirth,
    bool IsActive,
    DateTimeOffset CreatedAt,
    List<ParentLinkDto> ParentLinks);

public record ParentLinkDto(
    Guid LinkId,
    Guid ParentId,
    string ParentName,
    string ParentPhone,
    string ParentEmail,
    string Relationship,
    DateTimeOffset LinkedAt);
