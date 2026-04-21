using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GradeHomeworkSubmission;

public class GradeHomeworkSubmissionCommandHandler
    : IRequestHandler<GradeHomeworkSubmissionCommand, GradeHomeworkSubmissionResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<GradeHomeworkSubmissionCommandHandler> _logger;

    public GradeHomeworkSubmissionCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<GradeHomeworkSubmissionCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<GradeHomeworkSubmissionResponse> Handle(
        GradeHomeworkSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher" && _currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only teachers and admins can grade homework submissions.");
        }

        var submission = await _context.HomeworkSubmissions
            .Include(s => s.Homework)
            .FirstOrDefaultAsync(s =>
                s.Id == request.SubmissionId &&
                s.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (submission is null || submission.Homework is null)
        {
            throw new NotFoundException("HomeworkSubmission", request.SubmissionId.ToString());
        }

        // Teachers can only grade submissions for homework they authored or
        // for classes they're assigned to. Admin bypasses the class check.
        if (_currentUserService.Role == "Teacher")
        {
            var isAuthor = submission.Homework.AssignedById == _currentUserService.UserId;

            var isAssigned = await _context.TeacherClassAssignments
                .AnyAsync(a =>
                    a.SchoolId == _currentUserService.SchoolId &&
                    a.TeacherId == _currentUserService.UserId &&
                    a.ClassId == submission.Homework.ClassId,
                    cancellationToken);

            if (!isAuthor && !isAssigned)
            {
                throw new ForbiddenException("You are not assigned to this homework's class.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        submission.Grade = request.Grade;
        submission.Feedback = request.Feedback;
        submission.GradedById = _currentUserService.UserId;
        submission.GradedAt = now;
        submission.Status = HomeworkSubmissionStatus.Graded;
        submission.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Homework submission {SubmissionId} graded by {UserId}",
            submission.Id, _currentUserService.UserId);

        return new GradeHomeworkSubmissionResponse(
            submission.Id,
            submission.Grade!,
            submission.Feedback,
            submission.GradedAt!.Value);
    }
}
