using MediatR;

namespace EduConnect.Api.Features.Attendance.CancelLeaveApplication;

public record CancelLeaveApplicationCommand(Guid LeaveApplicationId) : IRequest<Unit>;

