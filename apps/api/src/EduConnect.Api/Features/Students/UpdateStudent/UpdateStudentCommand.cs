namespace EduConnect.Api.Features.Students.UpdateStudent;

public record UpdateStudentCommand(
    Guid Id,
    string Name,
    Guid ClassId,
    DateOnly? DateOfBirth) : IRequest<UpdateStudentResponse>;

public record UpdateStudentResponse(Guid StudentId, string Message);
