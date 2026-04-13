namespace EduConnect.Api.Features.Parents.CreateParent;

public record CreateParentCommand(
    string Name,
    string Phone,
    string Email,
    string Pin) : IRequest<CreateParentResponse>;

public record CreateParentResponse(Guid ParentId, string Message);
