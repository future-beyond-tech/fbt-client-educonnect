using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Phase 1.2 — the GET attachments handler gained a third entity-type
/// branch: <c>homework_submission</c>. These tests pin the four role
/// matrices (Admin / Teacher / Parent / other) plus cross-tenant denial.
/// </summary>
public class HomeworkSubmissionAttachmentAclTests
{
    [Fact]
    public async Task Admin_can_view_any_submission_attachment_in_tenant()
    {
        var f = await Seed();

        var admin = CurrentUser(f.SchoolId, "Admin");
        var attachments = await RunHandler(f.Options, admin, f.SubmissionId);

        attachments.Should().ContainSingle();
        attachments[0].FileName.Should().Be("submission.pdf");
    }

    [Fact]
    public async Task Teacher_who_authored_homework_can_view_submission_attachments()
    {
        var f = await Seed();

        var teacher = CurrentUser(f.SchoolId, "Teacher", f.AuthorTeacherId);
        var attachments = await RunHandler(f.Options, teacher, f.SubmissionId);

        attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Teacher_assigned_to_homework_class_can_view_submission_attachments()
    {
        var f = await Seed();

        var teacher = CurrentUser(f.SchoolId, "Teacher", f.AssignedTeacherId);
        var attachments = await RunHandler(f.Options, teacher, f.SubmissionId);

        attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Teacher_neither_author_nor_assigned_is_forbidden()
    {
        var f = await Seed();

        var teacher = CurrentUser(f.SchoolId, "Teacher", f.OutsideTeacherId);
        var act = () => RunHandler(f.Options, teacher, f.SubmissionId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not have access to these submission attachments.");
    }

    [Fact]
    public async Task Parent_linked_to_student_can_view_their_childs_submission_attachments()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var attachments = await RunHandler(f.Options, parent, f.SubmissionId);

        attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Parent_not_linked_to_student_is_forbidden()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.OtherParentId);
        var act = () => RunHandler(f.Options, parent, f.SubmissionId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not have access to these submission attachments.");
    }

    [Fact]
    public async Task Cross_tenant_user_cannot_see_submission()
    {
        var f = await Seed();

        var otherTenantAdmin = CurrentUser(Guid.NewGuid(), "Admin");
        var act = () => RunHandler(f.Options, otherTenantAdmin, f.SubmissionId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Submission_that_does_not_exist_raises_NotFound()
    {
        var f = await Seed();

        var admin = CurrentUser(f.SchoolId, "Admin");
        var act = () => RunHandler(f.Options, admin, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static async Task<List<AttachmentDto>> RunHandler(
        DbContextOptions<AppDbContext> options,
        CurrentUserService currentUser,
        Guid submissionId)
    {
        await using var context = new AppDbContext(options, currentUser);
        var storage = new Mock<IStorageService>(MockBehavior.Loose);
        storage
            .Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed.example.com/file");

        var handler = new GetAttachmentsForEntityQueryHandler(
            context,
            currentUser,
            storage.Object);

        return await handler.Handle(
            new GetAttachmentsForEntityQuery(submissionId, "homework_submission"),
            CancellationToken.None);
    }

    private static CurrentUserService CurrentUser(Guid schoolId, string role, Guid? userId = null) => new()
    {
        SchoolId = schoolId,
        UserId = userId ?? Guid.NewGuid(),
        Role = role,
        Name = role,
    };

    private static async Task<Fixture> Seed()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var authorTeacherId = Guid.NewGuid();
        var assignedTeacherId = Guid.NewGuid();
        var outsideTeacherId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var otherParentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SubmissionAcl_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var ctx = new AppDbContext(options);
        ctx.Schools.Add(new SchoolEntity
        {
            Id = schoolId,
            Name = "Test School",
            Code = "TST",
            Address = "",
            ContactPhone = "",
            ContactEmail = "",
        });
        ctx.Classes.Add(new ClassEntity
        {
            Id = classId,
            SchoolId = schoolId,
            Name = "7",
            Section = "A",
            AcademicYear = "2026",
        });

        ctx.Users.AddRange(
            new UserEntity { Id = authorTeacherId,   SchoolId = schoolId, Phone = "09000000001", Name = "Author Teacher",   Role = "Teacher" },
            new UserEntity { Id = assignedTeacherId, SchoolId = schoolId, Phone = "09000000002", Name = "Assigned Teacher", Role = "Teacher" },
            new UserEntity { Id = outsideTeacherId,  SchoolId = schoolId, Phone = "09000000003", Name = "Outside Teacher",  Role = "Teacher" },
            new UserEntity { Id = parentId,          SchoolId = schoolId, Phone = "09000000004", Name = "Parent",           Role = "Parent"  },
            new UserEntity { Id = otherParentId,     SchoolId = schoolId, Phone = "09000000005", Name = "Other Parent",     Role = "Parent"  });

        ctx.Students.AddRange(
            new StudentEntity { Id = studentId,      SchoolId = schoolId, ClassId = classId, Name = "Child",       RollNumber = "001", IsActive = true },
            new StudentEntity { Id = otherStudentId, SchoolId = schoolId, ClassId = classId, Name = "Other Child", RollNumber = "002", IsActive = true });

        ctx.ParentStudentLinks.AddRange(
            new ParentStudentLinkEntity { Id = Guid.NewGuid(), SchoolId = schoolId, ParentId = parentId,      StudentId = studentId,      Relationship = "parent" },
            new ParentStudentLinkEntity { Id = Guid.NewGuid(), SchoolId = schoolId, ParentId = otherParentId, StudentId = otherStudentId, Relationship = "parent" });

        ctx.TeacherClassAssignments.Add(new TeacherClassAssignmentEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            TeacherId = assignedTeacherId,
            ClassId = classId,
            Subject = "Math",
            IsClassTeacher = false,
        });

        ctx.Homeworks.Add(new HomeworkEntity
        {
            Id = homeworkId,
            SchoolId = schoolId,
            ClassId = classId,
            Subject = "Math",
            Title = "HW",
            Description = "",
            AssignedById = authorTeacherId,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = "Published",
        });

        ctx.HomeworkSubmissions.Add(new HomeworkSubmissionEntity
        {
            Id = submissionId,
            SchoolId = schoolId,
            HomeworkId = homeworkId,
            StudentId = studentId,
            Status = HomeworkSubmissionStatus.Submitted,
            BodyText = "My answer",
            SubmittedAt = DateTimeOffset.UtcNow,
        });

        ctx.Attachments.Add(new AttachmentEntity
        {
            Id = attachmentId,
            SchoolId = schoolId,
            EntityId = submissionId,
            EntityType = "homework_submission",
            StorageKey = $"{schoolId}/submission/{attachmentId}/submission.pdf",
            FileName = "submission.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            UploadedById = parentId,
            UploadedAt = DateTimeOffset.UtcNow,
            Status = AttachmentStatus.Available,
        });

        await ctx.SaveChangesAsync();

        return new Fixture(
            options,
            schoolId,
            authorTeacherId,
            assignedTeacherId,
            outsideTeacherId,
            parentId,
            otherParentId,
            submissionId);
    }

    private sealed record Fixture(
        DbContextOptions<AppDbContext> Options,
        Guid SchoolId,
        Guid AuthorTeacherId,
        Guid AssignedTeacherId,
        Guid OutsideTeacherId,
        Guid ParentId,
        Guid OtherParentId,
        Guid SubmissionId);
}
