namespace EduConnect.Api.Features.Teachers.GetClassesForTeacher;

public record GetClassesForTeacherQuery() : IRequest<List<TeacherClassDto>>;

public record TeacherClassDto(
    Guid ClassId,
    string ClassName,
    string Section,
    string Subject);
