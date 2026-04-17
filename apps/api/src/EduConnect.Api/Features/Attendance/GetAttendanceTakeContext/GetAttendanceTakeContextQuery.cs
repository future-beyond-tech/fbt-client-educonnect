using MediatR;

namespace EduConnect.Api.Features.Attendance.GetAttendanceTakeContext;

public record GetAttendanceTakeContextQuery(Guid ClassId, DateOnly Date)
    : IRequest<GetAttendanceTakeContextResponse>;

public record TakeStudentDto(Guid Id, string Name, string RollNumber);

public record TakeExceptionDto(Guid StudentId, string Status, string? Reason);

public record TakeLeaveDto(
    Guid LeaveId,
    Guid StudentId,
    string StudentName,
    string RollNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string Status);

public record GetAttendanceTakeContextResponse(
    Guid ClassId,
    DateOnly Date,
    // True when the caller is the assigned class teacher for this class and may
    // submit attendance. False when the caller is a subject teacher (or other
    // staff) with a read-only view. Authoritative source for the client — do
    // not rely on client-side role flags alone.
    bool CanEdit,
    List<TakeStudentDto> Students,
    List<TakeExceptionDto> Exceptions,
    List<TakeLeaveDto> ApprovedLeaves,
    List<TakeLeaveDto> PendingLeaves);

