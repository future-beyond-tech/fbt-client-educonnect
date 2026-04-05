namespace EduConnect.Api.Features.Teachers.AssignClassToTeacher;

public record AssignClassToTeacherCommand(
    Guid TeacherId,
    Guid ClassId,
    string Subject) : IRequest<AssignClassToTeacherResponse>;

public record AssignClassToTeacherResponse(Guid AssignmentId, string Message);
