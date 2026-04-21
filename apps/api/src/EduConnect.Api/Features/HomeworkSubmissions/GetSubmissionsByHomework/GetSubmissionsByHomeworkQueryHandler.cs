using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GetSubmissionsByHomework;

public class GetSubmissionsByHomeworkQueryHandler
    : IRequestHandler<GetSubmissionsByHomeworkQuery, IReadOnlyList<HomeworkSubmissionListItem>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetSubmissionsByHomeworkQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<HomeworkSubmissionListItem>> Handle(
        GetSubmissionsByHomeworkQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher" && _currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only teachers and admins can view homework submissions.");
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

        if (_currentUserService.Role == "Teacher")
        {
            var isAuthor = homework.AssignedById == _currentUserService.UserId;
            var isAssigned = await _context.TeacherClassAssignments
                .AnyAsync(a =>
                    a.SchoolId == _currentUserService.SchoolId &&
                    a.TeacherId == _currentUserService.UserId &&
                    a.ClassId == homework.ClassId,
                    cancellationToken);

            if (!isAuthor && !isAssigned)
            {
                throw new ForbiddenException("You are not assigned to this homework's class.");
            }
        }

        var rows = await (
            from s in _context.HomeworkSubmissions
            join stu in _context.Students on s.StudentId equals stu.Id
            where s.HomeworkId == request.HomeworkId
                  && s.SchoolId == _currentUserService.SchoolId
            orderby s.SubmittedAt
            select new HomeworkSubmissionListItem(
                s.Id,
                stu.Id,
                stu.Name,
                stu.RollNumber,
                s.Status,
                s.BodyText,
                s.Grade,
                s.Feedback,
                s.SubmittedAt,
                s.GradedAt))
            .ToListAsync(cancellationToken);

        return rows;
    }
}
