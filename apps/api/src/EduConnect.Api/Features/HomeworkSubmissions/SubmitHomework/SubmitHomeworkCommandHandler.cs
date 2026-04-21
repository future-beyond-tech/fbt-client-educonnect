using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.SubmitHomework;

public class SubmitHomeworkCommandHandler
    : IRequestHandler<SubmitHomeworkCommand, SubmitHomeworkResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<SubmitHomeworkCommandHandler> _logger;

    public SubmitHomeworkCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<SubmitHomeworkCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<SubmitHomeworkResponse> Handle(
        SubmitHomeworkCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Parent")
        {
            throw new ForbiddenException("Only parents can submit homework on behalf of a student.");
        }

        // The parent must be linked to this student.
        var link = await _context.ParentStudentLinks
            .FirstOrDefaultAsync(l =>
                l.SchoolId == _currentUserService.SchoolId &&
                l.ParentId == _currentUserService.UserId &&
                l.StudentId == request.StudentId,
                cancellationToken);

        if (link is null)
        {
            throw new ForbiddenException("You can only submit homework for your own child.");
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s =>
                s.Id == request.StudentId &&
                s.SchoolId == _currentUserService.SchoolId &&
                s.IsActive,
                cancellationToken);
        if (student is null)
        {
            throw new NotFoundException("Student", request.StudentId.ToString());
        }

        var homework = await _context.Homeworks
            .FirstOrDefaultAsync(h =>
                h.Id == request.HomeworkId &&
                h.SchoolId == _currentUserService.SchoolId &&
                !h.IsDeleted,
                cancellationToken);
        if (homework is null)
        {
            throw new NotFoundException("Homework", request.HomeworkId.ToString());
        }

        if (homework.Status != "Published")
        {
            throw new InvalidOperationException("Homework is not yet available for submission.");
        }

        if (homework.ClassId != student.ClassId)
        {
            throw new ForbiddenException("This homework is for a different class.");
        }

        var now = DateTimeOffset.UtcNow;
        var isLate = DateOnly.FromDateTime(now.UtcDateTime) > homework.DueDate;
        var nextStatus = isLate ? HomeworkSubmissionStatus.Late : HomeworkSubmissionStatus.Submitted;

        // Upsert: one active submission per (homework, student).
        var submission = await _context.HomeworkSubmissions
            .FirstOrDefaultAsync(s =>
                s.HomeworkId == request.HomeworkId &&
                s.StudentId == request.StudentId,
                cancellationToken);

        if (submission is null)
        {
            submission = new HomeworkSubmissionEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                HomeworkId = request.HomeworkId,
                StudentId = request.StudentId,
                BodyText = request.BodyText,
                Status = nextStatus,
                SubmittedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _context.HomeworkSubmissions.Add(submission);
        }
        else
        {
            // Teacher already graded — resubmission would overwrite feedback.
            // Block here; a proper Return-for-redo flow (Phase 6 follow-up)
            // will explicitly clear grade state and allow re-submission.
            if (submission.Status == HomeworkSubmissionStatus.Graded)
            {
                throw new InvalidOperationException(
                    "This submission has already been graded.");
            }

            submission.BodyText = request.BodyText;
            submission.Status = nextStatus;
            submission.SubmittedAt = now;
            submission.UpdatedAt = now;
        }

        // Link any just-uploaded attachments to this submission. Only
        // attachments the parent themselves uploaded are accepted — cross-
        // tenant is already blocked by the SchoolId check, but we also
        // prevent a parent from claiming another user's attachment.
        if (request.AttachmentIds is { Count: > 0 } attachmentIds)
        {
            var attachments = await _context.Attachments
                .Where(a =>
                    attachmentIds.Contains(a.Id) &&
                    a.SchoolId == _currentUserService.SchoolId &&
                    a.UploadedById == _currentUserService.UserId &&
                    (a.EntityId == null || a.EntityId == submission.Id))
                .ToListAsync(cancellationToken);

            if (attachments.Count != attachmentIds.Count)
            {
                throw new ForbiddenException("One or more attachments are not available for this submission.");
            }

            foreach (var attachment in attachments)
            {
                attachment.EntityId = submission.Id;
                attachment.EntityType = "homework_submission";
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Homework submission {SubmissionId} recorded for student {StudentId} on homework {HomeworkId} with status {Status}",
            submission.Id, submission.StudentId, submission.HomeworkId, submission.Status);

        return new SubmitHomeworkResponse(submission.Id, submission.Status, submission.SubmittedAt);
    }
}
