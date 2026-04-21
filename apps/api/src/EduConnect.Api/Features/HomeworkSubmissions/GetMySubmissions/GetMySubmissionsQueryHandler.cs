using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.HomeworkSubmissions.GetMySubmissions;

public class GetMySubmissionsQueryHandler
    : IRequestHandler<GetMySubmissionsQuery, IReadOnlyList<MyHomeworkSubmissionItem>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetMySubmissionsQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<MyHomeworkSubmissionItem>> Handle(
        GetMySubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Parent")
        {
            throw new ForbiddenException("Only parents can view their children's submissions.");
        }

        // The parent's accessible student set = all students linked to them.
        var myStudentIds = await _context.ParentStudentLinks
            .Where(l =>
                l.SchoolId == _currentUserService.SchoolId &&
                l.ParentId == _currentUserService.UserId)
            .Select(l => l.StudentId)
            .ToListAsync(cancellationToken);

        if (myStudentIds.Count == 0)
        {
            return Array.Empty<MyHomeworkSubmissionItem>();
        }

        if (request.StudentId.HasValue && !myStudentIds.Contains(request.StudentId.Value))
        {
            throw new ForbiddenException("You can only view submissions for your own children.");
        }

        var scopedStudentIds = request.StudentId.HasValue
            ? new[] { request.StudentId.Value }
            : myStudentIds.ToArray();

        var rows = await (
            from s in _context.HomeworkSubmissions
            join hw in _context.Homeworks on s.HomeworkId equals hw.Id
            join stu in _context.Students on s.StudentId equals stu.Id
            where s.SchoolId == _currentUserService.SchoolId
                  && scopedStudentIds.Contains(s.StudentId)
                  && !hw.IsDeleted
            orderby s.SubmittedAt descending
            select new MyHomeworkSubmissionItem(
                s.Id,
                hw.Id,
                hw.Title,
                hw.Subject,
                stu.Id,
                stu.Name,
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
