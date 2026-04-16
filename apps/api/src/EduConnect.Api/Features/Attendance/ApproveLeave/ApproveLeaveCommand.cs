using MediatR;

namespace EduConnect.Api.Features.Attendance.ApproveLeave;

public record ApproveLeaveCommand(Guid LeaveApplicationId) : IRequest<ApproveLeaveResponse>;

public record ApproveLeaveResponse(string Message);

