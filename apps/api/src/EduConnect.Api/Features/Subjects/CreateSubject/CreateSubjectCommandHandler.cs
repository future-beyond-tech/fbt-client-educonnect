using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Subjects.CreateSubject;

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, CreateSubjectResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<CreateSubjectCommandHandler> _logger;

    public CreateSubjectCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<CreateSubjectCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CreateSubjectResponse> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can create subjects.");
        }

        var trimmedName = request.Name.Trim();

        var exists = await _context.Subjects
            .AnyAsync(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.Name.ToLower() == trimmedName.ToLower(),
                cancellationToken);

        if (exists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Name", new[] { $"Subject '{trimmedName}' already exists." } }
            });
        }

        var subject = new SubjectEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Name = trimmedName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Subject created: {SubjectId} '{SubjectName}' by admin {AdminId}",
            subject.Id, trimmedName, _currentUserService.UserId);

        return new CreateSubjectResponse(subject.Id, "Subject created successfully.");
    }
}
