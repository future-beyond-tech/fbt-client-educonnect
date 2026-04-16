using MediatR;

namespace EduConnect.Api.Features.Attendance.RejectLeave;

public record RejectLeaveCommand(Guid LeaveApplicationId, string ReviewNote) : IRequest<RejectLeaveResponse>;

public record RejectLeaveResponse(string Message);

