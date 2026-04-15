namespace EduConnect.Api.Features.Attendance.MarkAbsence;

public record MarkAbsenceCommand(string RollNumber, DateOnly Date, string? Reason = null) : IRequest<MarkAbsenceResponse>;

public record MarkAbsenceResponse(Guid RecordId, string Status, string Message);
