namespace EduConnect.Api.Features.Teachers.RemoveClassFromTeacher;

public record RemoveClassFromTeacherCommand(
    Guid TeacherId,
    Guid AssignmentId) : IRequest<RemoveClassFromTeacherResponse>;

public record RemoveClassFromTeacherResponse(string Message);
