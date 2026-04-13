using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Homework.ApproveHomework;

public class ApproveHomeworkCommandHandler : IRequestHandler<ApproveHomeworkCommand, ApproveHomeworkResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApproveHomeworkCommandHandler> _logger;

    public ApproveHomeworkCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        ILogger<ApproveHomeworkCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApproveHomeworkResponse> Handle(ApproveHomeworkCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can approve homework.");
        }

        var homework = await _context.Homeworks
            .FirstOrDefaultAsync(h =>
                h.Id == request.HomeworkId &&
                h.SchoolId == _currentUserService.SchoolId &&
                !h.IsDeleted,
                cancellationToken);

        if (homework == null)
        {
            throw new NotFoundException("Homework", request.HomeworkId.ToString());
        }

        if (homework.Status != "PendingApproval")
        {
            throw new InvalidOperationException("Only homework pending approval can be approved.");
        }

        var isClassTeacher = await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.ClassId == homework.ClassId &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.IsClassTeacher,
                cancellationToken);

        if (!isClassTeacher)
        {
            throw new ForbiddenException("Only the class teacher can approve homework for this class.");
        }

        homework.Status = "Published";
        homework.ApprovedAt = DateTimeOffset.UtcNow;
        homework.ApprovedById = _currentUserService.UserId;
        homework.PublishedAt = DateTimeOffset.UtcNow;
        homework.IsEditable = false;

        await _context.SaveChangesAsync(cancellationToken);

        var parentIds = await _context.ParentStudentLinks
            .Where(psl => psl.SchoolId == _currentUserService.SchoolId
                          && psl.Student != null
                          && psl.Student.ClassId == homework.ClassId
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
                $"New Homework: {homework.Title}",
                $"{homework.Subject} — due {homework.DueDate:yyyy-MM-dd}",
                homework.Id,
                "homework",
                cancellationToken);
        }

        _logger.LogInformation(
            "Homework {HomeworkId} approved/published by class teacher {TeacherId}, notified {Count} parents",
            request.HomeworkId, _currentUserService.UserId, parentIds.Count);

        return new ApproveHomeworkResponse("Homework approved and published.");
    }
}

