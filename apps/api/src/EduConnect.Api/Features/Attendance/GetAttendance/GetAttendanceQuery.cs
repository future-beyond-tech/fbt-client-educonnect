namespace EduConnect.Api.Features.Attendance.GetAttendance;

public record GetAttendanceQuery(Guid? StudentId = null, int? Month = null, int? Year = null) : IRequest<List<AttendanceDto>>;

public record AttendanceDto(
    Guid RecordId,
    Guid StudentId,
    DateOnly Date,
    string Status,
    string? Reason,
    string EnteredByRole,
    DateTimeOffset CreatedAt);
