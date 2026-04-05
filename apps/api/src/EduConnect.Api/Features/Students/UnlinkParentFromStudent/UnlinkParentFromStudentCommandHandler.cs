using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Students.UnlinkParentFromStudent;

public class UnlinkParentFromStudentCommandHandler : IRequestHandler<UnlinkParentFromStudentCommand, UnlinkParentFromStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UnlinkParentFromStudentCommandHandler> _logger;

    public UnlinkParentFromStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UnlinkParentFromStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UnlinkParentFromStudentResponse> Handle(UnlinkParentFromStudentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can unlink parents from students.");
        }

        // IDOR guard: Verify linkId belongs to the specified studentId AND is in this school
        var link = await _context.ParentStudentLinks
            .FirstOrDefaultAsync(psl =>
                psl.Id == request.LinkId &&
                psl.StudentId == request.StudentId &&
                psl.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (link == null)
        {
            throw new NotFoundException(
                $"Parent-student link with ID {request.LinkId} not found for student {request.StudentId}.");
        }

        _context.ParentStudentLinks.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Parent {ParentId} unlinked from student {StudentId} (link {LinkId}) by admin {AdminId}",
            link.ParentId, request.StudentId, request.LinkId, _currentUserService.UserId);

        return new UnlinkParentFromStudentResponse("Parent unlinked from student successfully.");
    }
}
