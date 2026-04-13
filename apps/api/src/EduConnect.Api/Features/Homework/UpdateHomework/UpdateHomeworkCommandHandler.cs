using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Homework.UpdateHomework;

public class UpdateHomeworkCommandHandler : IRequestHandler<UpdateHomeworkCommand, UpdateHomeworkResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpdateHomeworkCommandHandler> _logger;

    public UpdateHomeworkCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpdateHomeworkCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpdateHomeworkResponse> Handle(UpdateHomeworkCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to update homework",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only teachers can update homework.");
        }

        var homework = await _context.Homeworks
            .FirstOrDefaultAsync(h =>
                h.Id == request.HomeworkId &&
                h.SchoolId == _currentUserService.SchoolId &&
                !h.IsDeleted,
                cancellationToken);

        if (homework == null)
        {
            _logger.LogWarning("Homework {HomeworkId} not found", request.HomeworkId);
            throw new NotFoundException("Homework", request.HomeworkId.ToString());
        }

        if (homework.AssignedById != _currentUserService.UserId)
        {
            _logger.LogWarning(
                "Teacher {TeacherId} attempted to update homework assigned by {AssignedById}",
                _currentUserService.UserId, homework.AssignedById);
            throw new ForbiddenException("You can only edit homework you created.");
        }

        if (homework.Status != "Draft" && homework.Status != "Rejected")
        {
            _logger.LogWarning(
                "Attempt to edit homework {HomeworkId} in status {Status}",
                request.HomeworkId, homework.Status);
            throw new ForbiddenException("Homework can only be edited while it is a draft or after it is rejected.");
        }

        homework.Title = request.Title;
        homework.Description = request.Description;
        homework.DueDate = request.DueDate;
        homework.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Homework {HomeworkId} updated by teacher {TeacherId}",
            request.HomeworkId, _currentUserService.UserId);

        return new UpdateHomeworkResponse("Homework updated successfully.");
    }
}
