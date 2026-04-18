namespace EduConnect.Api.Features.Teachers.CreateTeacher;

public record CreateTeacherCommand(
    string Name,
    string Phone,
    string Email,
    string Password,
    string Role = "Teacher",
    Guid? ClassId = null,
    string? Subject = null,
    bool IsClassTeacher = false) : IRequest<CreateTeacherResponse>;

public record CreateTeacherResponse(Guid TeacherId, string Message, string TemporaryPassword);
