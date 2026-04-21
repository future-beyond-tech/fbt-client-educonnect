using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.UpdateExam;

public class UpdateExamCommandHandler : IRequestHandler<UpdateExamCommand, UpdateExamResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UpdateExamCommandHandler> _logger;

    public UpdateExamCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UpdateExamCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<UpdateExamResponse> Handle(UpdateExamCommand request, CancellationToken cancellationToken)
    {
        // Only the class teacher for the exam's class may edit it, and only
        // while the schedule is still a draft. Once published, parents have
        // the schedule, so the contract is "delete + recreate" or send an
        // amended schedule notice via the Notices feature.
        if (_currentUserService.Role != "Teacher")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to update an exam",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only teachers can update exams.");
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
            _logger.LogWarning(
                "Teacher {TeacherId} attempted to update exam {ExamId} but is not the class teacher",
                _currentUserService.UserId, request.ExamId);
            throw new ForbiddenException("Only the class teacher can update this exam.");
        }

        if (exam.IsSchedulePublished)
        {
            _logger.LogWarning(
                "Attempt to edit already-published exam {ExamId}", exam.Id);
            throw new InvalidOperationException(
                "Exam schedule has already been published and cannot be edited. " +
                "Delete and recreate, or send an amended schedule notice.");
        }

        var now = DateTimeOffset.UtcNow;

        exam.Name = request.Name.Trim();
        exam.AcademicYear = request.AcademicYear.Trim();
        exam.UpdatedAt = now;

        // Replace all subject rows atomically. Safe because results cannot
        // exist before the schedule is published, and this path is blocked
        // once IsSchedulePublished = true.
        _context.ExamSubjects.RemoveRange(exam.Subjects);

        var newSubjects = request.Subjects
            .Select(s => new ExamSubjectEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _currentUserService.SchoolId,
                ExamId = exam.Id,
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

        _context.ExamSubjects.AddRange(newSubjects);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Exam {ExamId} updated by {TeacherId} (draft). {SubjectCount} subjects.",
            exam.Id, _currentUserService.UserId, newSubjects.Count);

        return new UpdateExamResponse("Exam schedule updated.");
    }
}
