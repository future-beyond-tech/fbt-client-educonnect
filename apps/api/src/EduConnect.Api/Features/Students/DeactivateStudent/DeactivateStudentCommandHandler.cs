using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Students.DeactivateStudent;

public class DeactivateStudentCommandHandler : IRequestHandler<DeactivateStudentCommand, DeactivateStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<DeactivateStudentCommandHandler> _logger;

    public DeactivateStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<DeactivateStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<DeactivateStudentResponse> Handle(DeactivateStudentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can deactivate students.");
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s =>
                s.Id == request.Id &&
                s.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (student == null)
        {
            throw new NotFoundException($"Student with ID {request.Id} not found.");
        }

        if (!student.IsActive)
        {
            return new DeactivateStudentResponse(student.Id, "Student is already deactivated.");
        }

        student.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Student deactivated: {StudentId} by admin {AdminId}",
            student.Id, _currentUserService.UserId);

        return new DeactivateStudentResponse(student.Id, "Student deactivated successfully.");
    }
}
