namespace EduConnect.Api.Features.Attendance.AdminOverride;

public record AdminOverrideCommand(Guid RecordId, string Reason) : IRequest<AdminOverrideResponse>;

public record AdminOverrideResponse(Guid NewRecordId, string Message);
