namespace EduConnect.Api.Features.Auth.LoginParent;

public record LoginParentCommand(string Phone, string Pin) : IRequest<LoginParentResponse>;

public record LoginParentResponse(string AccessToken);
