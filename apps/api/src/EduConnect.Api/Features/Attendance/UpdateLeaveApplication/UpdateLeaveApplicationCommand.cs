using MediatR;

namespace EduConnect.Api.Features.Attendance.UpdateLeaveApplication;

public record UpdateLeaveApplicationCommand(
    Guid LeaveApplicationId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason) : IRequest<UpdateLeaveApplicationResponse>;

public record UpdateLeaveApplicationResponse(string Message);

