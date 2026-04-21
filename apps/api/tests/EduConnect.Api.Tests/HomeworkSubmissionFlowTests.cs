using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.HomeworkSubmissions.GetMySubmissions;
using EduConnect.Api.Features.HomeworkSubmissions.GradeHomeworkSubmission;
using EduConnect.Api.Features.HomeworkSubmissions.SubmitHomework;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EduConnect.Api.Tests;

public class HomeworkSubmissionFlowTests
{
    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly Guid _classId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _parentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public async Task Submit_happy_path_creates_Submitted_record()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        var parent = CurrentUser("Parent", _parentId);
        await using var context = new AppDbContext(options, parent);
        var handler = new SubmitHomeworkCommandHandler(context, parent, NullLogger<SubmitHomeworkCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubmitHomeworkCommand(homeworkId, _studentId, "My answer", null),
            CancellationToken.None);

        result.Status.Should().Be(HomeworkSubmissionStatus.Submitted);

        var saved = await context.HomeworkSubmissions.SingleAsync();
        saved.StudentId.Should().Be(_studentId);
        saved.HomeworkId.Should().Be(homeworkId);
        saved.BodyText.Should().Be("My answer");
    }

    [Fact]
    public async Task Submit_after_due_date_records_Late_status()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: -1);

        var parent = CurrentUser("Parent", _parentId);
        await using var context = new AppDbContext(options, parent);
        var handler = new SubmitHomeworkCommandHandler(context, parent, NullLogger<SubmitHomeworkCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubmitHomeworkCommand(homeworkId, _studentId, "Late but here", null),
            CancellationToken.None);

        result.Status.Should().Be(HomeworkSubmissionStatus.Late);
    }

    [Fact]
    public async Task Submit_as_unlinked_parent_is_forbidden()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        var strangerParentId = Guid.NewGuid();
        var stranger = CurrentUser("Parent", strangerParentId);
        await using var context = new AppDbContext(options, stranger);
        // Parent exists as a user but has no ParentStudentLink.
        context.Users.Add(new UserEntity
        {
            Id = strangerParentId,
            SchoolId = _schoolId,
            Phone = "09000001111",
            Name = "Unrelated Parent",
            Role = "Parent",
        });
        await context.SaveChangesAsync();

        var handler = new SubmitHomeworkCommandHandler(context, stranger, NullLogger<SubmitHomeworkCommandHandler>.Instance);

        var act = async () => await handler.Handle(
            new SubmitHomeworkCommand(homeworkId, _studentId, "hack", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You can only submit homework for your own child.");
    }

    [Fact]
    public async Task Submit_by_non_parent_role_is_forbidden()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        var teacher = CurrentUser("Teacher", _teacherId);
        await using var context = new AppDbContext(options, teacher);
        var handler = new SubmitHomeworkCommandHandler(context, teacher, NullLogger<SubmitHomeworkCommandHandler>.Instance);

        var act = async () => await handler.Handle(
            new SubmitHomeworkCommand(homeworkId, _studentId, "teacher tried to submit", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only parents can submit homework on behalf of a student.");
    }

    [Fact]
    public async Task Submit_twice_upserts_and_keeps_single_row()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        var parent = CurrentUser("Parent", _parentId);
        await using var context = new AppDbContext(options, parent);
        var handler = new SubmitHomeworkCommandHandler(context, parent, NullLogger<SubmitHomeworkCommandHandler>.Instance);

        await handler.Handle(
            new SubmitHomeworkCommand(homeworkId, _studentId, "first", null),
            CancellationToken.None);
        var second = await handler.Handle(
            new SubmitHomeworkCommand(homeworkId, _studentId, "second", null),
            CancellationToken.None);

        (await context.HomeworkSubmissions.CountAsync()).Should().Be(1);
        var row = await context.HomeworkSubmissions.SingleAsync();
        row.BodyText.Should().Be("second");
        row.Id.Should().Be(second.SubmissionId);
    }

    [Fact]
    public async Task Teacher_who_authored_homework_can_grade()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        // Seed a submission first via the parent handler.
        var parent = CurrentUser("Parent", _parentId);
        await using (var context = new AppDbContext(options, parent))
        {
            var submitHandler = new SubmitHomeworkCommandHandler(context, parent, NullLogger<SubmitHomeworkCommandHandler>.Instance);
            await submitHandler.Handle(
                new SubmitHomeworkCommand(homeworkId, _studentId, "done", null),
                CancellationToken.None);
        }

        Guid submissionId;
        await using (var readCtx = new AppDbContext(options, parent))
        {
            submissionId = (await readCtx.HomeworkSubmissions.SingleAsync()).Id;
        }

        var teacher = CurrentUser("Teacher", _teacherId);
        await using var gradeCtx = new AppDbContext(options, teacher);
        var gradeHandler = new GradeHomeworkSubmissionCommandHandler(
            gradeCtx, teacher, NullLogger<GradeHomeworkSubmissionCommandHandler>.Instance);

        var result = await gradeHandler.Handle(
            new GradeHomeworkSubmissionCommand(submissionId, "A", "Good work"),
            CancellationToken.None);

        result.Grade.Should().Be("A");
        var row = await gradeCtx.HomeworkSubmissions.SingleAsync();
        row.Status.Should().Be(HomeworkSubmissionStatus.Graded);
        row.GradedById.Should().Be(_teacherId);
    }

    [Fact]
    public async Task Teacher_not_assigned_and_not_author_cannot_grade()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        var parent = CurrentUser("Parent", _parentId);
        await using (var context = new AppDbContext(options, parent))
        {
            var submit = new SubmitHomeworkCommandHandler(context, parent, NullLogger<SubmitHomeworkCommandHandler>.Instance);
            await submit.Handle(new SubmitHomeworkCommand(homeworkId, _studentId, "done", null), CancellationToken.None);
        }

        Guid submissionId;
        await using (var read = new AppDbContext(options, parent))
        {
            submissionId = (await read.HomeworkSubmissions.SingleAsync()).Id;
        }

        // A different teacher who isn't the author and has no class assignment.
        var otherTeacherId = Guid.NewGuid();
        await using (var ctx = new AppDbContext(options, parent))
        {
            ctx.Users.Add(new UserEntity
            {
                Id = otherTeacherId,
                SchoolId = _schoolId,
                Phone = "09000002222",
                Name = "Other Teacher",
                Role = "Teacher",
            });
            await ctx.SaveChangesAsync();
        }

        var otherTeacher = CurrentUser("Teacher", otherTeacherId);
        await using var gradeCtx = new AppDbContext(options, otherTeacher);
        var gradeHandler = new GradeHomeworkSubmissionCommandHandler(
            gradeCtx, otherTeacher, NullLogger<GradeHomeworkSubmissionCommandHandler>.Instance);

        var act = async () => await gradeHandler.Handle(
            new GradeHomeworkSubmissionCommand(submissionId, "A", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not assigned to this homework's class.");
    }

    [Fact]
    public async Task GetMySubmissions_returns_only_parents_own_childrens_rows()
    {
        var options = CreateOptions();
        var homeworkId = await SeedStandardFixtureAsync(options, dueInDays: 3);

        // Seed a second student + parent in the same school. Parent A's query
        // must not leak Parent B's child's submission.
        var otherParentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        await using (var ctx = new AppDbContext(options, CurrentUser("Parent", _parentId)))
        {
            ctx.Users.Add(new UserEntity
            {
                Id = otherParentId,
                SchoolId = _schoolId,
                Phone = "09000003333",
                Name = "Other Parent",
                Role = "Parent",
            });
            ctx.Students.Add(new StudentEntity
            {
                Id = otherStudentId,
                SchoolId = _schoolId,
                ClassId = _classId,
                Name = "Other Student",
                RollNumber = "002",
                IsActive = true,
            });
            ctx.ParentStudentLinks.Add(new ParentStudentLinkEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = _schoolId,
                ParentId = otherParentId,
                StudentId = otherStudentId,
                Relationship = "parent",
            });
            await ctx.SaveChangesAsync();
        }

        // Both parents submit.
        var parentA = CurrentUser("Parent", _parentId);
        await using (var ctx = new AppDbContext(options, parentA))
        {
            var submit = new SubmitHomeworkCommandHandler(ctx, parentA, NullLogger<SubmitHomeworkCommandHandler>.Instance);
            await submit.Handle(new SubmitHomeworkCommand(homeworkId, _studentId, "A-answer", null), CancellationToken.None);
        }
        var parentB = CurrentUser("Parent", otherParentId);
        await using (var ctx = new AppDbContext(options, parentB))
        {
            var submit = new SubmitHomeworkCommandHandler(ctx, parentB, NullLogger<SubmitHomeworkCommandHandler>.Instance);
            await submit.Handle(new SubmitHomeworkCommand(homeworkId, otherStudentId, "B-answer", null), CancellationToken.None);
        }

        await using var readCtx = new AppDbContext(options, parentA);
        var handler = new GetMySubmissionsQueryHandler(readCtx, parentA);
        var rows = await handler.Handle(new GetMySubmissionsQuery(null), CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].StudentId.Should().Be(_studentId);
        rows[0].BodyText.Should().Be("A-answer");
    }

    // ── fixture helpers ─────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private CurrentUserService CurrentUser(string role, Guid userId) => new()
    {
        SchoolId = _schoolId,
        UserId = userId,
        Role = role,
        Name = role,
    };

    private async Task<Guid> SeedStandardFixtureAsync(
        DbContextOptions<AppDbContext> options,
        int dueInDays)
    {
        var homeworkId = Guid.NewGuid();

        await using var ctx = new AppDbContext(options, new CurrentUserService());

        ctx.Schools.Add(new SchoolEntity
        {
            Id = _schoolId,
            Name = "School",
            Code = "S1",
            Address = "",
            ContactPhone = "",
            ContactEmail = "",
        });
        ctx.Classes.Add(new ClassEntity
        {
            Id = _classId,
            SchoolId = _schoolId,
            Name = "6",
            Section = "A",
            AcademicYear = "2026",
        });
        ctx.Users.Add(new UserEntity
        {
            Id = _parentId,
            SchoolId = _schoolId,
            Phone = "09000000001",
            Name = "Parent",
            Role = "Parent",
        });
        ctx.Users.Add(new UserEntity
        {
            Id = _teacherId,
            SchoolId = _schoolId,
            Phone = "09000000002",
            Name = "Teacher",
            Role = "Teacher",
        });
        ctx.Students.Add(new StudentEntity
        {
            Id = _studentId,
            SchoolId = _schoolId,
            ClassId = _classId,
            Name = "Student",
            RollNumber = "001",
            IsActive = true,
        });
        ctx.ParentStudentLinks.Add(new ParentStudentLinkEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _schoolId,
            ParentId = _parentId,
            StudentId = _studentId,
            Relationship = "parent",
        });
        ctx.Homeworks.Add(new HomeworkEntity
        {
            Id = homeworkId,
            SchoolId = _schoolId,
            ClassId = _classId,
            Subject = "Math",
            Title = "HW",
            Description = "",
            AssignedById = _teacherId,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dueInDays)),
            Status = "Published",
        });

        await ctx.SaveChangesAsync();
        return homeworkId;
    }
}
