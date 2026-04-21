using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.UpsertExamResults;

public class UpsertExamResultsCommandHandler
    : IRequestHandler<UpsertExamResultsCommand, UpsertExamResultsResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpsertExamResultsCommandHandler> _logger;

    public UpsertExamResultsCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpsertExamResultsCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpsertExamResultsResponse> Handle(
        UpsertExamResultsCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only teachers can record exam results.");
        }

        var exam = await _context.Exams
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
            throw new ForbiddenException("Only the class teacher can record results for this exam.");
        }

        if (!exam.IsSchedulePublished)
        {
            throw new InvalidOperationException(
                "Results can only be recorded after the schedule is published.");
        }

        if (exam.IsResultsFinalized)
        {
            throw new InvalidOperationException(
                "Results have already been finalized. Finalized results are immutable.");
        }

        // Valid subject IDs for this exam — reject rows targeting unrelated subjects.
        var subjectIds = exam.Subjects.Select(s => s.Id).ToHashSet();
        var subjectMaxMarks = exam.Subjects.ToDictionary(s => s.Id, s => s.MaxMarks);

        // Valid students — must belong to the exam's class and be active.
        var studentIds = request.Rows.Select(r => r.StudentId).Distinct().ToList();
        var validStudentIds = await _context.Students
            .Where(s =>
                s.SchoolId == _currentUserService.SchoolId &&
                s.ClassId == exam.ClassId &&
                s.IsActive &&
                studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var validStudentIdSet = validStudentIds.ToHashSet();

        // Existing results for this exam — keyed by (ExamSubjectId, StudentId).
        // The DB unique index on (ExamSubjectId, StudentId) guarantees this
        // tuple is the natural key for upsert targeting.
        var existing = await _context.ExamResults
            .Where(r => r.SchoolId == _currentUserService.SchoolId && r.ExamId == exam.Id)
            .ToListAsync(cancellationToken);
        var existingByKey = existing
            .ToDictionary(r => (r.ExamSubjectId, r.StudentId));

        var warnings = new List<string>();
        var now = DateTimeOffset.UtcNow;
        var insertedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;

        foreach (var row in request.Rows)
        {
            // Skip rows targeting subjects that don't belong to this exam.
            if (!subjectIds.Contains(row.ExamSubjectId))
            {
                warnings.Add($"Row for student {row.StudentId} skipped: exam subject {row.ExamSubjectId} does not belong to this exam.");
                skippedCount++;
                continue;
            }

            if (!validStudentIdSet.Contains(row.StudentId))
            {
                warnings.Add($"Row for student {row.StudentId} skipped: student is not an active member of the exam's class.");
                skippedCount++;
                continue;
            }

            // Reject marks exceeding max for the subject.
            if (row.MarksObtained.HasValue &&
                subjectMaxMarks.TryGetValue(row.ExamSubjectId, out var max) &&
                row.MarksObtained.Value > max)
            {
                warnings.Add($"Row for student {row.StudentId}, subject {row.ExamSubjectId} skipped: marks {row.MarksObtained} exceed max {max}.");
                skippedCount++;
                continue;
            }

            var key = (row.ExamSubjectId, row.StudentId);
            if (existingByKey.TryGetValue(key, out var existingResult))
            {
                existingResult.MarksObtained = row.IsAbsent ? null : row.MarksObtained;
                existingResult.Grade = string.IsNullOrWhiteSpace(row.Grade) ? null : row.Grade.Trim();
                existingResult.Remarks = string.IsNullOrWhiteSpace(row.Remarks) ? null : row.Remarks.Trim();
                existingResult.IsAbsent = row.IsAbsent;
                existingResult.RecordedById = _currentUserService.UserId;
                existingResult.UpdatedAt = now;
                updatedCount++;
            }
            else
            {
                var newResult = new ExamResultEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = _currentUserService.SchoolId,
                    ExamId = exam.Id,
                    ExamSubjectId = row.ExamSubjectId,
                    StudentId = row.StudentId,
                    MarksObtained = row.IsAbsent ? null : row.MarksObtained,
                    Grade = string.IsNullOrWhiteSpace(row.Grade) ? null : row.Grade.Trim(),
                    Remarks = string.IsNullOrWhiteSpace(row.Remarks) ? null : row.Remarks.Trim(),
                    IsAbsent = row.IsAbsent,
                    RecordedById = _currentUserService.UserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                _context.ExamResults.Add(newResult);
                existingByKey[key] = newResult;
                insertedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Exam {ExamId} results upserted by {TeacherId}: {Inserted} new, {Updated} updated, {Skipped} skipped",
            exam.Id, _currentUserService.UserId, insertedCount, updatedCount, skippedCount);

        return new UpsertExamResultsResponse(insertedCount, updatedCount, skippedCount, warnings);
    }
}
