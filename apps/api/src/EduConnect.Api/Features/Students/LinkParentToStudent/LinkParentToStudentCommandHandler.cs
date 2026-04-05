using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Students.LinkParentToStudent;

public class LinkParentToStudentCommandHandler : IRequestHandler<LinkParentToStudentCommand, LinkParentToStudentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<LinkParentToStudentCommandHandler> _logger;

    public LinkParentToStudentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<LinkParentToStudentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<LinkParentToStudentResponse> Handle(LinkParentToStudentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can link parents to students.");
        }

        // Verify student exists in this school
        var studentExists = await _context.Students
            .AnyAsync(s =>
                s.Id == request.StudentId &&
                s.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (!studentExists)
        {
            throw new NotFoundException($"Student with ID {request.StudentId} not found.");
        }

        // Verify parent exists, is in same school, and has Parent role
        var parent = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == request.ParentId &&
                u.SchoolId == _currentUserService.SchoolId &&
                u.Role == "Parent" &&
                u.IsActive,
                cancellationToken);

        if (parent == null)
        {
            throw new NotFoundException("No active parent account found with the given ID in this school.");
        }

        // Check if link already exists
        var linkExists = await _context.ParentStudentLinks
            .AnyAsync(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.ParentId == request.ParentId &&
                psl.StudentId == request.StudentId,
                cancellationToken);

        if (linkExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "ParentId", new[] { "This parent is already linked to this student." } }
            });
        }

        var link = new ParentStudentLinkEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            ParentId = request.ParentId,
            StudentId = request.StudentId,
            Relationship = request.Relationship.ToLower(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ParentStudentLinks.Add(link);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Parent {ParentId} linked to student {StudentId} with relationship '{Relationship}' by admin {AdminId}",
            request.ParentId, request.StudentId, request.Relationship, _currentUserService.UserId);

        return new LinkParentToStudentResponse(link.Id, "Parent linked to student successfully.");
    }
}
