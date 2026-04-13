using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Homework.CreateHomework;

public class CreateHomeworkCommandHandler : IRequestHandler<CreateHomeworkCommand, CreateHomeworkResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<CreateHomeworkCommandHandler> _logger;

    public CreateHomeworkCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<CreateHomeworkCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CreateHomeworkResponse> Handle(CreateHomeworkCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to create homework",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only teachers can create homework.");
        }

        var assignment = await _context.TeacherClassAssignments
            .FirstOrDefaultAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.ClassId == request.ClassId &&
                tca.Subject == request.Subject,
                cancellationToken);

        if (assignment == null)
        {
            _logger.LogWarning(
                "Teacher {TeacherId} attempted to create homework for unassigned class {ClassId} and subject {Subject}",
                _currentUserService.UserId, request.ClassId, request.Subject);
            throw new ForbiddenException("You are not assigned to teach this subject in this class.");
        }

        var homework = new HomeworkEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            ClassId = request.ClassId,
            Subject = request.Subject,
            Title = request.Title,
            Description = request.Description,
            AssignedById = _currentUserService.UserId,
            DueDate = request.DueDate,
            Status = "Draft",
            PublishedAt = null,
            IsEditable = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Homeworks.Add(homework);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Homework created (draft): {HomeworkId} for class {ClassId} by teacher {TeacherId}",
            homework.Id, request.ClassId, _currentUserService.UserId);

        return new CreateHomeworkResponse(homework.Id, "Homework saved as draft. Submit for approval to publish.");
    }
}
