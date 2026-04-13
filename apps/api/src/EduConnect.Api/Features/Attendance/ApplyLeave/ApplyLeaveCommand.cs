namespace EduConnect.Api.Features.Attendance.ApplyLeave;

public record ApplyLeaveCommand(
    Guid StudentId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason
) : IRequest<ApplyLeaveResponse>;

public record ApplyLeaveResponse(Guid LeaveApplicationId, string Status, string Message);
