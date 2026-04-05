namespace EduConnect.Api.Features.Students.DeactivateStudent;

public record DeactivateStudentCommand(Guid Id) : IRequest<DeactivateStudentResponse>;

public record DeactivateStudentResponse(Guid StudentId, string Message);
