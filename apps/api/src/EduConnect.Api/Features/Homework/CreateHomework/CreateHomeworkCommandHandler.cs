using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Homework.CreateHomework;

public class CreateHomeworkCommandHandler : IRequestHandler<CreateHomeworkCommand, CreateHomeworkResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreateHomeworkCommandHandler> _logger;

    public CreateHomeworkCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<CreateHomeworkCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
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
            PublishedAt = DateTimeOffset.UtcNow,
            IsEditable = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Homeworks.Add(homework);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify parents of active students in this class
        var parentIds = await _context.ParentStudentLinks
            .Where(psl => psl.SchoolId == _currentUserService.SchoolId
                && psl.Student != null
                && psl.Student.ClassId == request.ClassId
                && psl.Student.IsActive)
            .Select(psl => psl.ParentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (parentIds.Count > 0)
        {
            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                parentIds,
                "homework_assigned",
                $"New Homework: {request.Title}",
                $"{request.Subject} — due {request.DueDate:yyyy-MM-dd}",
                homework.Id,
                "homework",
                cancellationToken);
        }

        _logger.LogInformation(
            "Homework created: {HomeworkId} for class {ClassId} by teacher {TeacherId}, notified {Count} parents",
            homework.Id, request.ClassId, _currentUserService.UserId, parentIds.Count);

        return new CreateHomeworkResponse(homework.Id, "Homework created successfully.");
    }
}
