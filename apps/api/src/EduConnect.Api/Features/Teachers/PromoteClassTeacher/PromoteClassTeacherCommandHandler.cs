using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Teachers.PromoteClassTeacher;

public class PromoteClassTeacherCommandHandler : IRequestHandler<PromoteClassTeacherCommand, PromoteClassTeacherResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<PromoteClassTeacherCommandHandler> _logger;

    public PromoteClassTeacherCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<PromoteClassTeacherCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<PromoteClassTeacherResponse> Handle(PromoteClassTeacherCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can update class teacher assignments.");
        }

        var assignment = await _context.TeacherClassAssignments
            .FirstOrDefaultAsync(tca =>
                tca.Id == request.AssignmentId &&
                tca.TeacherId == request.TeacherId &&
                tca.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (assignment == null)
        {
            throw new NotFoundException("Teacher assignment", request.AssignmentId.ToString());
        }

        var existingClassTeacher = await _context.TeacherClassAssignments
            .FirstOrDefaultAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.ClassId == assignment.ClassId &&
                tca.IsClassTeacher &&
                tca.Id != assignment.Id,
                cancellationToken);

        if (existingClassTeacher != null)
        {
            existingClassTeacher.IsClassTeacher = false;
            
            // Save immediately to avoid Postgres partial unique constraint formulation
            // issues when swapping true -> false and false -> true in the same batch
            await _context.SaveChangesAsync(cancellationToken);
        }

        assignment.IsClassTeacher = true;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assignment {AssignmentId} promoted to class teacher by admin {AdminId}",
            assignment.Id,
            _currentUserService.UserId);

        return new PromoteClassTeacherResponse(assignment.Id, "Class teacher updated successfully.");
    }
}
