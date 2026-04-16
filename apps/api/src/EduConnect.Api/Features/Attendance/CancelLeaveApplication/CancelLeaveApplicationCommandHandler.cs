using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Attendance.CancelLeaveApplication;

public class CancelLeaveApplicationCommandHandler : IRequestHandler<CancelLeaveApplicationCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<CancelLeaveApplicationCommandHandler> _logger;

    public CancelLeaveApplicationCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<CancelLeaveApplicationCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Unit> Handle(CancelLeaveApplicationCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Parent")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to cancel leave application {LeaveId}",
                _currentUserService.UserId, _currentUserService.Role, request.LeaveApplicationId);
            throw new ForbiddenException("Only parents can cancel leave applications.");
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
            throw new ForbiddenException("You can only cancel leave requests you created.");
        }

        if (leave.Status != "Pending")
        {
            throw new ForbiddenException("Only pending leave requests can be cancelled.");
        }

        leave.IsDeleted = true;
        leave.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Leave application {LeaveId} cancelled by parent {ParentId}",
            request.LeaveApplicationId, _currentUserService.UserId);

        return Unit.Value;
    }
}

