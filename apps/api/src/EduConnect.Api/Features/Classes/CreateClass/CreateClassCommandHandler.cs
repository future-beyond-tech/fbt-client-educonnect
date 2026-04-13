using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Classes.CreateClass;

public class CreateClassCommandHandler : IRequestHandler<CreateClassCommand, CreateClassResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<CreateClassCommandHandler> _logger;

    public CreateClassCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<CreateClassCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CreateClassResponse> Handle(CreateClassCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can create classes.");
        }

        var trimmedName = request.Name.Trim();
        var trimmedSection = request.Section.Trim();
        var trimmedAcademicYear = request.AcademicYear.Trim();

        var duplicateExists = await _context.Classes
            .AnyAsync(c =>
                c.SchoolId == _currentUserService.SchoolId &&
                c.Name.ToLower() == trimmedName.ToLower() &&
                c.Section.ToLower() == trimmedSection.ToLower() &&
                c.AcademicYear.ToLower() == trimmedAcademicYear.ToLower(),
                cancellationToken);

        if (duplicateExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                {
                    "Name",
                    new[]
                    {
                        $"Class '{trimmedName} {trimmedSection}' for academic year '{trimmedAcademicYear}' already exists."
                    }
                }
            });
        }

        var classEntity = new ClassEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Name = trimmedName,
            Section = trimmedSection,
            AcademicYear = trimmedAcademicYear,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Classes.Add(classEntity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Class {ClassId} created by admin {AdminId}",
            classEntity.Id,
            _currentUserService.UserId);

        return new CreateClassResponse(classEntity.Id, "Class created successfully.");
    }
}
