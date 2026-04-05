namespace EduConnect.Api.Features.Students.UnlinkParentFromStudent;

public record UnlinkParentFromStudentCommand(
    Guid StudentId,
    Guid LinkId) : IRequest<UnlinkParentFromStudentResponse>;

public record UnlinkParentFromStudentResponse(string Message);
