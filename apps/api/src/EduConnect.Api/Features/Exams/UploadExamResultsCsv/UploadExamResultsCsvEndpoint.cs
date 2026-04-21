using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Exams.UpsertExamResults;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.UploadExamResultsCsv;

/// <summary>
/// Multipart/form-data endpoint: accepts a `file` form field (CSV) and
/// translates it into an UpsertExamResultsCommand by resolving
/// roll-number + subject-name against the target exam's class and subjects.
///
/// This keeps the upload path thin: the canonical write happens through
/// the Upsert handler, which enforces role, publish state, and per-row
/// validation.
/// </summary>
public static class UploadExamResultsCsvEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        HttpRequest request,
        IMediator mediator,
        AppDbContext context,
        CurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Request must be multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "A non-empty 'file' is required." });
        }

        // Resolve the exam to map roll-number/subject-name -> IDs.
        var exam = await context.Exams
            .Include(e => e.Subjects)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exam", id.ToString());

        var students = await context.Students
            .Where(s => s.SchoolId == currentUser.SchoolId &&
                        s.ClassId == exam.ClassId &&
                        s.IsActive)
            .Select(s => new { s.Id, s.RollNumber })
            .ToListAsync(cancellationToken);

        var studentsByRoll = students.ToDictionary(
            s => s.RollNumber.Trim(),
            s => s.Id,
            StringComparer.OrdinalIgnoreCase);

        var subjectsByName = exam.Subjects.ToDictionary(
            s => s.Subject.Trim(),
            s => s.Id,
            StringComparer.OrdinalIgnoreCase);

        await using var stream = file.OpenReadStream();
        var parseResult = ExamResultsCsvParser.Parse(stream);

        if (parseResult.Errors.Count > 0 && parseResult.Rows.Count == 0)
        {
            return Results.BadRequest(new
            {
                message = "CSV had errors and no valid rows were found.",
                errors = parseResult.Errors,
            });
        }

        var unresolvedWarnings = new List<string>();
        var rows = new List<ExamResultRowInput>(parseResult.Rows.Count);

        foreach (var parsed in parseResult.Rows)
        {
            if (!studentsByRoll.TryGetValue(parsed.RollNumber, out var studentId))
            {
                unresolvedWarnings.Add(
                    $"Line {parsed.LineNumber}: roll_number '{parsed.RollNumber}' not found in this class.");
                continue;
            }

            if (!subjectsByName.TryGetValue(parsed.Subject, out var subjectId))
            {
                unresolvedWarnings.Add(
                    $"Line {parsed.LineNumber}: subject '{parsed.Subject}' is not a paper in this exam.");
                continue;
            }

            rows.Add(new ExamResultRowInput(
                StudentId: studentId,
                ExamSubjectId: subjectId,
                MarksObtained: parsed.MarksObtained,
                Grade: parsed.Grade,
                Remarks: parsed.Remarks,
                IsAbsent: parsed.IsAbsent));
        }

        if (rows.Count == 0)
        {
            return Results.BadRequest(new
            {
                message = "No CSV rows could be resolved to students and subjects in this exam.",
                errors = parseResult.Errors,
                warnings = unresolvedWarnings,
            });
        }

        var command = new UpsertExamResultsCommand(id, rows);
        var upsertResponse = await mediator.Send(command, cancellationToken);

        // Combine parse-stage + resolution-stage + domain-stage warnings so
        // the client can surface a single rollup.
        var combinedWarnings = new List<string>();
        combinedWarnings.AddRange(parseResult.Errors);
        combinedWarnings.AddRange(unresolvedWarnings);
        combinedWarnings.AddRange(upsertResponse.Warnings);

        return Results.Ok(new
        {
            inserted = upsertResponse.InsertedCount,
            updated = upsertResponse.UpdatedCount,
            skipped = upsertResponse.SkippedCount + unresolvedWarnings.Count + parseResult.Errors.Count,
            warnings = combinedWarnings,
        });
    }
}
