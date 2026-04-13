using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Homework.SubmitHomeworkForApproval;

public class SubmitHomeworkForApprovalCommandHandler
    : IRequestHandler<SubmitHomeworkForApprovalCommand, SubmitHomeworkForApprovalResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<SubmitHomeworkForApprovalCommandHandler> _logger;

    public SubmitHomeworkForApprovalCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<SubmitHomeworkForApprovalCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<SubmitHomeworkForApprovalResponse> Handle(
        SubmitHomeworkForApprovalCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can submit homework for approval.");
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

        if (homework.AssignedById != _currentUserService.UserId)
        {
            throw new ForbiddenException("You can only submit homework you created.");
        }

        if (homework.Status != "Draft" && homework.Status != "Rejected")
        {
            throw new InvalidOperationException("Only draft or rejected homework can be submitted for approval.");
        }

        homework.Status = "PendingApproval";
        homework.SubmittedAt = DateTimeOffset.UtcNow;
        homework.IsEditable = false;
        homework.RejectedAt = null;
        homework.RejectedById = null;
        homework.RejectedReason = null;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Homework {HomeworkId} submitted for approval by teacher {TeacherId}",
            request.HomeworkId, _currentUserService.UserId);

        return new SubmitHomeworkForApprovalResponse("Homework submitted for approval.");
    }
}

