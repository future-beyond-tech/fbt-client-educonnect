namespace EduConnect.Api.Features.Attachments.AttachFileToEntity;

public record AttachFileToEntityCommand(
    Guid AttachmentId,
    Guid EntityId,
    string EntityType) : IRequest<AttachFileToEntityResponse>;

public record AttachFileToEntityResponse(string Message);
