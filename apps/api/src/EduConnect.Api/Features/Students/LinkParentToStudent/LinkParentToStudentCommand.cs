namespace EduConnect.Api.Features.Students.LinkParentToStudent;

public record LinkParentToStudentCommand(
    Guid StudentId,
    Guid ParentId,
    string Relationship = "parent") : IRequest<LinkParentToStudentResponse>;

public record LinkParentToStudentResponse(Guid LinkId, string Message);
