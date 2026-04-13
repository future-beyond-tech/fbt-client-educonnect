using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Classes.UpdateClass;

public class UpdateClassCommandHandler : IRequestHandler<UpdateClassCommand, UpdateClassResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpdateClassCommandHandler> _logger;

    public UpdateClassCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpdateClassCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpdateClassResponse> Handle(UpdateClassCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can update classes.");
        }

        var classEntity = await _context.Classes
            .FirstOrDefaultAsync(c =>
                c.Id == request.Id &&
                c.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (classEntity == null)
        {
            throw new NotFoundException($"Class with ID {request.Id} not found.");
        }

        var trimmedName = request.Name.Trim();
        var trimmedSection = request.Section.Trim();
        var trimmedAcademicYear = request.AcademicYear.Trim();

        var duplicateExists = await _context.Classes
            .AnyAsync(c =>
                c.Id != request.Id &&
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

        classEntity.Name = trimmedName;
        classEntity.Section = trimmedSection;
        classEntity.AcademicYear = trimmedAcademicYear;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Class {ClassId} updated by admin {AdminId}",
            classEntity.Id,
            _currentUserService.UserId);

        return new UpdateClassResponse(classEntity.Id, "Class updated successfully.");
    }
}
