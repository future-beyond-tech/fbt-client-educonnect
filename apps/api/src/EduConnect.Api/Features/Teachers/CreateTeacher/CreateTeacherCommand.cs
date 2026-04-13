namespace EduConnect.Api.Features.Teachers.CreateTeacher;

public record CreateTeacherCommand(
    string Name,
    string Phone,
    string Email,
    string Password) : IRequest<CreateTeacherResponse>;

public record CreateTeacherResponse(Guid TeacherId, string Message);
