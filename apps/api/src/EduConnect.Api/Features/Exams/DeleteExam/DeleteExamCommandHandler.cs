using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.DeleteExam;

public class DeleteExamCommandHandler : IRequestHandler<DeleteExamCommand, DeleteExamResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<DeleteExamCommandHandler> _logger;

    public DeleteExamCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<DeleteExamCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<DeleteExamResponse> Handle(DeleteExamCommand request, CancellationToken cancellationToken)
    {
        // Deletion is a soft-delete. Policy:
        //   - Class teacher can delete a draft exam outright.
        //   - Class teacher can soft-delete a published schedule only if
        //     results have NOT been finalized (published-but-draft-results is
        //     the typical "schedule sent but exam cancelled" flow).
        //   - Once results are finalized, the exam is a permanent record and
        //     can no longer be deleted (admin can soft-delete via future
        //     admin tooling if needed).
        if (_currentUserService.Role != "Teacher" && _currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only teachers or admins can delete exams.");
        }

        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == request.ExamId && !e.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exam", request.ExamId.ToString());

        if (_currentUserService.Role == "Teacher")
        {
            var isClassTeacher = await _context.TeacherClassAssignments
                .AnyAsync(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId &&
                    tca.ClassId == exam.ClassId &&
                    tca.IsClassTeacher,
                    cancellationToken);

            if (!isClassTeacher)
            {
                throw new ForbiddenException("Only the class teacher can delete this exam.");
            }
        }

        if (exam.IsResultsFinalized)
        {
            _logger.LogWarning(
                "Attempt to delete exam {ExamId} with finalized results",
                exam.Id);
            throw new InvalidOperationException(
                "Exam results have been finalized. Finalized exams cannot be deleted.");
        }

        var now = DateTimeOffset.UtcNow;
        exam.IsDeleted = true;
        exam.DeletedAt = now;
        exam.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Exam {ExamId} soft-deleted by user {UserId}",
            exam.Id, _currentUserService.UserId);

        return new DeleteExamResponse("Exam deleted.");
    }
}
