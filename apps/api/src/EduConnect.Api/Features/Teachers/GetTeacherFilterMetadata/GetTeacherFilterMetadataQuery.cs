namespace EduConnect.Api.Features.Teachers.GetTeacherFilterMetadata;

public record GetTeacherFilterMetadataQuery : IRequest<TeacherFilterMetadataDto>;

public record TeacherFilterMetadataDto(List<string> Subjects);
