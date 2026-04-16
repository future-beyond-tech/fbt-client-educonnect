using MediatR;

namespace EduConnect.Api.Features.Attendance.SubmitAttendanceTake;

public record SubmitAttendanceTakeCommand(
    Guid ClassId,
    DateOnly Date,
    List<SubmitAttendanceItem> Items) : IRequest<SubmitAttendanceTakeResponse>;

public record SubmitAttendanceItem(
    Guid StudentId,
    string Status,
    string? Reason);

public record SubmitAttendanceTakeResponse(
    int CreatedCount,
    int UpdatedCount,
    int ClearedCount,
    string Message);

