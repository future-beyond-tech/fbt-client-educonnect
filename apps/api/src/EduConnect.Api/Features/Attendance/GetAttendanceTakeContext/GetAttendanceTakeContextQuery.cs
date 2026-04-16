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
    List<TakeStudentDto> Students,
    List<TakeExceptionDto> Exceptions,
    List<TakeLeaveDto> ApprovedLeaves,
    List<TakeLeaveDto> PendingLeaves);

