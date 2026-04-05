using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Teachers.RemoveClassFromTeacher;

public class RemoveClassFromTeacherCommandHandler : IRequestHandler<RemoveClassFromTeacherCommand, RemoveClassFromTeacherResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<RemoveClassFromTeacherCommandHandler> _logger;

    public RemoveClassFromTeacherCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<RemoveClassFromTeacherCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<RemoveClassFromTeacherResponse> Handle(RemoveClassFromTeacherCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can remove class assignments.");
        }

        // IDOR guard: assignment must belong to the specified teacher AND this school
        var assignment = await _context.TeacherClassAssignments
            .FirstOrDefaultAsync(tca =>
                tca.Id == request.AssignmentId &&
                tca.TeacherId == request.TeacherId &&
                tca.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (assignment == null)
        {
            throw new NotFoundException(
                $"Assignment with ID {request.AssignmentId} not found for teacher {request.TeacherId}.");
        }

        _context.TeacherClassAssignments.Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assignment {AssignmentId} removed from teacher {TeacherId} by admin {AdminId}",
            request.AssignmentId, request.TeacherId, _currentUserService.UserId);

        return new RemoveClassFromTeacherResponse("Assignment removed successfully.");
    }
}
