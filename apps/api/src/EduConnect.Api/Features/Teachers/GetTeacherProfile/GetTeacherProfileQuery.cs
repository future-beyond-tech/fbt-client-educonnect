namespace EduConnect.Api.Features.Teachers.GetTeacherProfile;

public record GetTeacherProfileQuery(Guid TeacherId) : IRequest<TeacherProfileDto>;

public record TeacherProfileDto(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    List<TeacherAssignmentDto> Assignments);

public record TeacherAssignmentDto(
    Guid AssignmentId,
    Guid ClassId,
    string ClassName,
    string Section,
    string Subject,
    bool IsClassTeacher,
    DateTimeOffset AssignedAt);
