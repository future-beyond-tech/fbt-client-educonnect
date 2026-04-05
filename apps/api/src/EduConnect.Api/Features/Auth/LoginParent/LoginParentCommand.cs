namespace EduConnect.Api.Features.Auth.LoginParent;

public record LoginParentCommand(string RollNumber, string Pin) : IRequest<LoginParentResponse>;

public record LoginParentResponse(string AccessToken);
