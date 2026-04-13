namespace EduConnect.Api.Features.Teachers.PromoteClassTeacher;

public record PromoteClassTeacherCommand(
    Guid TeacherId,
    Guid AssignmentId) : IRequest<PromoteClassTeacherResponse>;

public record PromoteClassTeacherResponse(Guid AssignmentId, string Message);
