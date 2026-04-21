using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Email;
using EduConnect.Api.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.PublishExamSchedule;

public class PublishExamScheduleCommandHandler
    : IRequestHandler<PublishExamScheduleCommand, PublishExamScheduleResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublishExamScheduleCommandHandler> _logger;

    public PublishExamScheduleCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<PublishExamScheduleCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PublishExamScheduleResponse> Handle(
        PublishExamScheduleCommand request,
        CancellationToken cancellationToken)
    {
        // Only the class teacher may publish. Admin is intentionally
        // excluded here: admins can publish notices via the Notices feature,
        // but exams are authored and published by class teachers.
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can publish exam schedules.");
        }

        var exam = await _context.Exams
            .Include(e => e.Class)
            .Include(e => e.Subjects.OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime))
            .FirstOrDefaultAsync(e => e.Id == request.ExamId && !e.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exam", request.ExamId.ToString());

        var isClassTeacher = await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.ClassId == exam.ClassId &&
                tca.IsClassTeacher,
                cancellationToken);

        if (!isClassTeacher)
        {
            throw new ForbiddenException("Only the class teacher can publish this exam schedule.");
        }

        if (exam.IsSchedulePublished)
        {
            _logger.LogWarning("Attempt to re-publish already-published exam {ExamId}", exam.Id);
            throw new InvalidOperationException("Exam schedule has already been published.");
        }

        if (exam.Subjects.Count == 0)
        {
            throw new InvalidOperationException("Cannot publish an exam with no subjects.");
        }

        var now = DateTimeOffset.UtcNow;
        exam.IsSchedulePublished = true;
        exam.SchedulePublishedAt = now;
        exam.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        // Resolve parents of every active student in the target class.
        // These are the primary recipients of the schedule.
        var parents = await _context.ParentStudentLinks
            .Where(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.Student != null &&
                psl.Student.ClassId == exam.ClassId &&
                psl.Student.IsActive)
            .Select(psl => new ParentRecipient(psl.ParentId, psl.Student!.Id, psl.Student.Name))
            .ToListAsync(cancellationToken);

        var parentUserIds = parents.Select(p => p.ParentId).Distinct().ToList();

        if (parentUserIds.Count > 0)
        {
            var title = $"Exam schedule: {exam.Name}";
            var body = BuildPushBody(exam);

            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                parentUserIds,
                "exam_schedule",
                title,
                body,
                exam.Id,
                "exam_schedule",
                cancellationToken);

            await SendScheduleEmailsAsync(exam, parents, cancellationToken);
        }

        _logger.LogInformation(
            "Exam {ExamId} schedule published by teacher {TeacherId}, notified {Count} parents",
            exam.Id, _currentUserService.UserId, parentUserIds.Count);

        return new PublishExamScheduleResponse(
            "Exam schedule published. Parents have been notified.",
            parentUserIds.Count);
    }

    private static string BuildPushBody(ExamEntity exam)
    {
        if (exam.Subjects.Count == 0)
        {
            return "The exam schedule has been published. Tap to view.";
        }

        var first = exam.Subjects.OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime).First();
        return $"First paper: {first.Subject} on {first.ExamDate:MMM d}. " +
               $"{exam.Subjects.Count} subject{(exam.Subjects.Count == 1 ? "" : "s")} total.";
    }

    private async Task SendScheduleEmailsAsync(
        ExamEntity exam,
        List<ParentRecipient> parents,
        CancellationToken cancellationToken)
    {
        try
        {
            var school = await _context.Schools
                .FirstOrDefaultAsync(s => s.Id == _currentUserService.SchoolId, cancellationToken);
            if (school is null)
            {
                return;
            }

            var parentIds = parents.Select(p => p.ParentId).Distinct().ToList();

            var parentEmails = await _context.Users
                .Where(u =>
                    u.SchoolId == _currentUserService.SchoolId &&
                    parentIds.Contains(u.Id) &&
                    u.IsActive &&
                    u.Email != null &&
                    u.Email != string.Empty)
                .Select(u => new { u.Id, u.Name, u.Email })
                .ToListAsync(cancellationToken);

            if (parentEmails.Count == 0)
            {
                return;
            }

            var appUrl = EmailBranding.ResolveAppUrl(_configuration);
            var logoUrl = EmailBranding.ResolveLogoUrl(_configuration);
            var viewUrl = $"{appUrl}/exams/{exam.Id}";
            var publishedAt = exam.SchedulePublishedAt ?? DateTimeOffset.UtcNow;

            var subjectLines = exam.Subjects
                .Select(s => new ExamScheduleSubjectLine(
                    s.Subject,
                    s.ExamDate,
                    s.StartTime,
                    s.EndTime,
                    s.MaxMarks,
                    s.Room))
                .ToList();

            var className = exam.Class?.Name ?? string.Empty;
            var section = exam.Class?.Section ?? string.Empty;

            // Build a ParentId -> one student name map for personalization.
            // If a parent has multiple kids in the class (twins, etc.), we
            // pick one; the email body still covers the whole class.
            var parentToStudent = parents
                .GroupBy(p => p.ParentId)
                .ToDictionary(g => g.Key, g => g.First().StudentName);

            var successCount = 0;
            var failureCount = 0;
            foreach (var recipient in parentEmails)
            {
                try
                {
                    parentToStudent.TryGetValue(recipient.Id, out var studentName);

                    var content = EmailTemplates.BuildExamSchedule(
                        school,
                        recipientName: recipient.Name,
                        studentName: studentName,
                        examName: exam.Name,
                        className: className,
                        section: section,
                        academicYear: exam.AcademicYear,
                        publishedAt: publishedAt,
                        subjects: subjectLines,
                        viewUrl: viewUrl,
                        logoUrl: logoUrl);

                    var ok = await _emailService.SendEmailAsync(
                        recipient.Email!,
                        content.Subject,
                        content.Html,
                        content.Text,
                        cancellationToken);

                    if (ok)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex,
                        "Failed to email exam schedule {ExamId} to parent {UserId}",
                        exam.Id, recipient.Id);
                }
            }

            _logger.LogInformation(
                "Exam {ExamId} schedule email dispatch: {Success} sent, {Failure} failed (of {Total} parents with email)",
                exam.Id, successCount, failureCount, parentEmails.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed while dispatching exam schedule emails for {ExamId}", exam.Id);
        }
    }

    private sealed record ParentRecipient(Guid ParentId, Guid StudentId, string StudentName);
}
