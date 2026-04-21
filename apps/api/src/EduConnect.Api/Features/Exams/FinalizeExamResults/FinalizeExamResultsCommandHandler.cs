using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Email;
using EduConnect.Api.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.FinalizeExamResults;

public class FinalizeExamResultsCommandHandler
    : IRequestHandler<FinalizeExamResultsCommand, FinalizeExamResultsResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FinalizeExamResultsCommandHandler> _logger;

    public FinalizeExamResultsCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        INotificationService notificationService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<FinalizeExamResultsCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<FinalizeExamResultsResponse> Handle(
        FinalizeExamResultsCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can finalize exam results.");
        }

        var exam = await _context.Exams
            .Include(e => e.Class)
            .Include(e => e.Subjects)
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
            throw new ForbiddenException("Only the class teacher can finalize results for this exam.");
        }

        if (!exam.IsSchedulePublished)
        {
            throw new InvalidOperationException(
                "Cannot finalize results before the schedule is published.");
        }

        if (exam.IsResultsFinalized)
        {
            throw new InvalidOperationException("Results have already been finalized.");
        }

        // Completeness check: every active student must have a row for every
        // subject (either a mark, a grade, or is_absent). The point of
        // "finalize" is that the data is a formal record.
        var students = await _context.Students
            .Where(s => s.SchoolId == _currentUserService.SchoolId &&
                        s.ClassId == exam.ClassId &&
                        s.IsActive)
            .Select(s => new { s.Id, s.Name, s.RollNumber })
            .ToListAsync(cancellationToken);

        var results = await _context.ExamResults
            .Where(r => r.SchoolId == _currentUserService.SchoolId && r.ExamId == exam.Id)
            .ToListAsync(cancellationToken);
        var resultKeys = results.Select(r => (r.ExamSubjectId, r.StudentId)).ToHashSet();

        var missing = new List<string>();
        foreach (var student in students)
        {
            foreach (var subject in exam.Subjects)
            {
                if (!resultKeys.Contains((subject.Id, student.Id)))
                {
                    missing.Add($"{student.Name} ({student.RollNumber}) — {subject.Subject}");
                    if (missing.Count >= 10)
                    {
                        break;
                    }
                }
            }
            if (missing.Count >= 10) break;
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Results are incomplete. Missing entries: " + string.Join("; ", missing) +
                (missing.Count >= 10 ? " (and possibly more)" : string.Empty));
        }

        var now = DateTimeOffset.UtcNow;
        exam.IsResultsFinalized = true;
        exam.ResultsFinalizedAt = now;
        exam.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        // Build per-student result packages so we can:
        //   - insert one notification per parent,
        //   - send one personalized email per parent.
        var resultsByStudent = results
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var subjectsById = exam.Subjects.ToDictionary(s => s.Id);

        var parentLinks = await _context.ParentStudentLinks
            .Where(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.Student != null &&
                psl.Student.ClassId == exam.ClassId &&
                psl.Student.IsActive)
            .Select(psl => new { psl.ParentId, psl.StudentId })
            .ToListAsync(cancellationToken);

        // Per-parent list of (student, result lines, totals) so a parent who
        // has multiple children in the same class gets one email per child.
        var perParent = parentLinks
            .GroupBy(p => p.ParentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).ToList());

        var allParentIds = perParent.Keys.ToList();

        if (allParentIds.Count > 0)
        {
            var title = $"Exam results: {exam.Name}";
            var body = $"Results for {exam.Name} are now available. Tap to view your child's report.";

            await _notificationService.SendBatchAsync(
                _currentUserService.SchoolId,
                allParentIds,
                "exam_result",
                title,
                body,
                exam.Id,
                "exam_result",
                cancellationToken);

            await SendResultEmailsAsync(
                exam,
                students.ToDictionary(s => s.Id, s => (s.Name, s.RollNumber)),
                resultsByStudent,
                subjectsById,
                perParent,
                now,
                cancellationToken);
        }

        _logger.LogInformation(
            "Exam {ExamId} results finalized by teacher {TeacherId}: {StudentCount} students, {ParentCount} parents notified",
            exam.Id, _currentUserService.UserId, students.Count, allParentIds.Count);

        return new FinalizeExamResultsResponse(
            "Results finalized. Parents have been notified.",
            students.Count,
            allParentIds.Count);
    }

    private async Task SendResultEmailsAsync(
        ExamEntity exam,
        Dictionary<Guid, (string Name, string RollNumber)> studentsById,
        Dictionary<Guid, List<ExamResultEntity>> resultsByStudent,
        Dictionary<Guid, ExamSubjectEntity> subjectsById,
        Dictionary<Guid, List<Guid>> perParentStudentIds,
        DateTimeOffset finalizedAt,
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

            var parentIds = perParentStudentIds.Keys.ToList();
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
            var className = exam.Class?.Name ?? string.Empty;
            var section = exam.Class?.Section ?? string.Empty;

            var successCount = 0;
            var failureCount = 0;

            foreach (var parent in parentEmails)
            {
                if (!perParentStudentIds.TryGetValue(parent.Id, out var studentIds))
                {
                    continue;
                }

                foreach (var studentId in studentIds)
                {
                    if (!studentsById.TryGetValue(studentId, out var studentInfo))
                    {
                        continue;
                    }

                    var studentResults = resultsByStudent.TryGetValue(studentId, out var list)
                        ? list
                        : new List<ExamResultEntity>();

                    var lines = exam.Subjects
                        .OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime)
                        .Select(s =>
                        {
                            var match = studentResults.FirstOrDefault(r => r.ExamSubjectId == s.Id);
                            if (match is not null)
                            {
                                return new ExamResultSubjectLine(
                                    s.Subject, match.MarksObtained, s.MaxMarks,
                                    match.Grade, match.IsAbsent);
                            }
                            return new ExamResultSubjectLine(s.Subject, null, s.MaxMarks, null, false);
                        })
                        .ToList();

                    var viewUrl = $"{appUrl}/exams/{exam.Id}/results?studentId={studentId}";

                    try
                    {
                        var content = EmailTemplates.BuildExamResult(
                            school,
                            parentName: parent.Name,
                            studentName: studentInfo.Name,
                            examName: exam.Name,
                            className: className,
                            section: section,
                            finalizedAt: finalizedAt,
                            subjects: lines,
                            viewUrl: viewUrl,
                            logoUrl: logoUrl);

                        var ok = await _emailService.SendEmailAsync(
                            parent.Email!,
                            content.Subject,
                            content.Html,
                            content.Text,
                            cancellationToken);

                        if (ok) successCount++; else failureCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex,
                            "Failed to email exam result for student {StudentId} to parent {ParentId}",
                            studentId, parent.Id);
                    }
                }
            }

            _logger.LogInformation(
                "Exam {ExamId} result email dispatch: {Success} sent, {Failure} failed",
                exam.Id, successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed while dispatching exam result emails for {ExamId}", exam.Id);
        }
    }
}
