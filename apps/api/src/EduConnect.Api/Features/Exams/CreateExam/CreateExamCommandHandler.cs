using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, CreateExamResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<CreateExamCommandHandler> _logger;

    public CreateExamCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<CreateExamCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CreateExamResponse> Handle(CreateExamCommand request, CancellationToken cancellationToken)
    {
        // Only the class teacher for this class may author a schedule.
        // Mirrors the "Teacher + TeacherClassAssignments" guard used in
        // CreateHomework, but tightens it to is_class_teacher=true because
        // the exam is a cross-subject artefact, not a single-subject one.
        if (_currentUserService.Role != "Teacher")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to create an exam",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only teachers can create exams.");
        }

        var isClassTeacher = await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.TeacherId == _currentUserService.UserId &&
                tca.ClassId == request.ClassId &&
                tca.IsClassTeacher,
                cancellationToken);

        if (!isClassTeacher)
        {
            _logger.LogWarning(
                "Teacher {TeacherId} attempted to create exam for class {ClassId} but is not the class teacher",
                _currentUserService.UserId, request.ClassId);
            throw new ForbiddenException("Only the class teacher can create exams for this class.");
        }

        var @class = await _context.Classes
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken)
            ?? throw new NotFoundException("Class", request.ClassId.ToString());

        var examId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var exam = new ExamEntity
        {
            Id = examId,
            SchoolId = _currentUserService.SchoolId,
            ClassId = request.ClassId,
            Name = request.Name.Trim(),
            AcademicYear = request.AcademicYear.Trim(),
            CreatedById = _currentUserService.UserId,
            IsSchedulePublished = false,
            IsResultsFinalized = false,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var subjects = request.Subjects
            .Select(s => new ExamSubjectEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                ExamId = examId,
                Subject = s.Subject.Trim(),
                ExamDate = s.ExamDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                MaxMarks = s.MaxMarks,
                Room = string.IsNullOrWhiteSpace(s.Room) ? null : s.Room.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        _context.Exams.Add(exam);
        _context.ExamSubjects.AddRange(subjects);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Exam {ExamId} created by {TeacherId} for class {ClassId} with {SubjectCount} subjects",
            examId, _currentUserService.UserId, request.ClassId, subjects.Count);

        return new CreateExamResponse(examId, "Exam schedule saved as draft. Publish to notify parents.");
    }
}
