namespace EduConnect.Api.Features.Students.EnrollStudent;

public record EnrollStudentCommand(
    string Name,
    string RollNumber,
    Guid ClassId,
    DateOnly? DateOfBirth) : IRequest<EnrollStudentResponse>;

public record EnrollStudentResponse(Guid StudentId, string Message);
