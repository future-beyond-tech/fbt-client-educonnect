namespace EduConnect.Api.Features.Students.EnrollStudent;

public record EnrollStudentParentRequest(
    string Name,
    string Phone,
    string Email,
    string Pin,
    string Relationship = "parent");

public record EnrollStudentExistingParentRequest(
    Guid ParentId,
    string Relationship = "parent");

public record EnrollStudentCommand(
    string Name,
    string RollNumber,
    Guid ClassId,
    DateOnly? DateOfBirth,
    EnrollStudentParentRequest? Parent = null,
    EnrollStudentExistingParentRequest? ExistingParent = null) : IRequest<EnrollStudentResponse>;

public record EnrollStudentResponse(Guid StudentId, string Message, string? TemporaryPin = null);
