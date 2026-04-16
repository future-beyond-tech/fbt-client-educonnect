using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Attendance.UpdateLeaveApplication;

public class UpdateLeaveApplicationCommandHandler
    : IRequestHandler<UpdateLeaveApplicationCommand, UpdateLeaveApplicationResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpdateLeaveApplicationCommandHandler> _logger;

    public UpdateLeaveApplicationCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpdateLeaveApplicationCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpdateLeaveApplicationResponse> Handle(
        UpdateLeaveApplicationCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Parent")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to update leave application {LeaveId}",
                _currentUserService.UserId, _currentUserService.Role, request.LeaveApplicationId);
            throw new ForbiddenException("Only parents can edit leave applications.");
        }

        var leave = await _context.LeaveApplications
            .FirstOrDefaultAsync(la =>
                la.Id == request.LeaveApplicationId &&
                la.SchoolId == _currentUserService.SchoolId &&
                !la.IsDeleted,
                cancellationToken);

        if (leave == null)
        {
            throw new NotFoundException("LeaveApplication", request.LeaveApplicationId.ToString());
        }

        if (leave.ParentId != _currentUserService.UserId)
        {
            throw new ForbiddenException("You can only edit leave requests you created.");
        }

        if (leave.Status != "Pending")
        {
            throw new ForbiddenException("Leave requests can only be edited while they are pending.");
        }

        leave.StartDate = request.StartDate;
        leave.EndDate = request.EndDate;
        leave.Reason = request.Reason;
        leave.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Leave application {LeaveId} updated by parent {ParentId}",
            request.LeaveApplicationId, _currentUserService.UserId);

        return new UpdateLeaveApplicationResponse("Leave request updated successfully.");
    }
}

