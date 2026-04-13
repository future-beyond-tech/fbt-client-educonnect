using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Homework.RejectHomework;

public class RejectHomeworkCommandHandler : IRequestHandler<RejectHomeworkCommand, RejectHomeworkResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<RejectHomeworkCommandHandler> _logger;

    public RejectHomeworkCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<RejectHomeworkCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<RejectHomeworkResponse> Handle(RejectHomeworkCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can reject homework.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Rejection reason is required.");
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
            throw new InvalidOperationException("Only homework pending approval can be rejected.");
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
            throw new ForbiddenException("Only the class teacher can reject homework for this class.");
        }

        homework.Status = "Rejected";
        homework.RejectedAt = DateTimeOffset.UtcNow;
        homework.RejectedById = _currentUserService.UserId;
        homework.RejectedReason = request.Reason.Trim();
        homework.IsEditable = true;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Homework {HomeworkId} rejected by class teacher {TeacherId}",
            request.HomeworkId, _currentUserService.UserId);

        return new RejectHomeworkResponse("Homework rejected.");
    }
}

