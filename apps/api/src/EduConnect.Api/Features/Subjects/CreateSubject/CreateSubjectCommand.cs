namespace EduConnect.Api.Features.Subjects.CreateSubject;

public record CreateSubjectCommand(string Name) : IRequest<CreateSubjectResponse>;

public record CreateSubjectResponse(Guid SubjectId, string Message);
