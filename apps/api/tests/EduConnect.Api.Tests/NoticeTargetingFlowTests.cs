using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Notices.CreateNotice;
using EduConnect.Api.Features.Notices.PublishNotice;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class NoticeTargetingFlowTests
{
    [Fact]
    public async Task CreateNotice_WithClassAudience_CreatesTargetsForEverySectionInTheClassGroup()
    {
        var schoolId = Guid.NewGuid();
        var classAId = Guid.NewGuid();
        var classBId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classAId, schoolId, "5", "A", "2026-27"),
                CreateClass(classBId, schoolId, "5", "B", "2026-27")
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new CreateNoticeCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<CreateNoticeCommandHandler>>());

        var response = await handler.Handle(
            new CreateNoticeCommand(
                "Exam schedule",
                "Classes 5A and 5B have a shared exam update.",
                "Class",
                [classAId, classBId]),
            CancellationToken.None);

        var notice = await context.Notices
            .Include(n => n.TargetClasses)
            .SingleAsync(n => n.Id == response.NoticeId);

        response.Message.Should().Be("Notice created as draft successfully.");
        notice.TargetAudience.Should().Be("Class");
        notice.TargetClasses.Select(target => target.ClassId).Should().BeEquivalentTo([classAId, classBId]);
    }

    [Fact]
    public async Task CreateNotice_WithClassAudience_MissingASection_ThrowsValidationException()
    {
        var schoolId = Guid.NewGuid();
        var classAId = Guid.NewGuid();
        var classBId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classAId, schoolId, "5", "A", "2026-27"),
                CreateClass(classBId, schoolId, "5", "B", "2026-27")
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new CreateNoticeCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<CreateNoticeCommandHandler>>());

        var act = () => handler.Handle(
            new CreateNoticeCommand(
                "Exam schedule",
                "Only one section was selected.",
                "Class",
                [classAId]),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("TargetClassIds");
    }

    [Fact]
    public async Task PublishNotice_WithSpecificSections_NotifiesOnlyUsersFromThoseSections()
    {
        var schoolId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var classAId = Guid.NewGuid();
        var classBId = Guid.NewGuid();
        var classCId = Guid.NewGuid();
        var teacherAId = Guid.NewGuid();
        var teacherCId = Guid.NewGuid();
        var parentBId = Guid.NewGuid();
        var parentCId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var studentCId = Guid.NewGuid();
        var noticeId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin", adminId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classAId, schoolId, "5", "A", "2026-27"),
                CreateClass(classBId, schoolId, "5", "B", "2026-27"),
                CreateClass(classCId, schoolId, "5", "C", "2026-27")
            ],
            users:
            [
                CreateUser(adminId, schoolId, "Admin", "08999999999", "Admin User", "admin@test.com"),
                CreateUser(teacherAId, schoolId, "Teacher", "08888888880", "Teacher A", "teachera@test.com"),
                CreateUser(teacherCId, schoolId, "Teacher", "08888888881", "Teacher C", "teacherc@test.com"),
                CreateUser(parentBId, schoolId, "Parent", "08777777770", "Parent B", "parentb@test.com"),
                CreateUser(parentCId, schoolId, "Parent", "08777777771", "Parent C", "parentc@test.com")
            ],
            students:
            [
                CreateStudent(studentBId, schoolId, classBId, "2026-5B-001", "Student B"),
                CreateStudent(studentCId, schoolId, classCId, "2026-5C-001", "Student C")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherAId,
                    ClassId = classAId,
                    Subject = "Math",
                    IsClassTeacher = true,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherCId,
                    ClassId = classCId,
                    Subject = "Math",
                    IsClassTeacher = true,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            parentLinks:
            [
                CreateParentLink(schoolId, parentBId, studentBId, "parent"),
                CreateParentLink(schoolId, parentCId, studentCId, "parent")
            ],
            notices:
            [
                new NoticeEntity
                {
                    Id = noticeId,
                    SchoolId = schoolId,
                    Title = "Section update",
                    Body = "Only sections A and B should receive this.",
                    TargetAudience = "Section",
                    PublishedById = adminId,
                    IsPublished = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ],
            noticeTargets:
            [
                CreateNoticeTarget(noticeId, classAId, schoolId),
                CreateNoticeTarget(noticeId, classBId, schoolId)
            ]);

        await using var context = new AppDbContext(options, currentUser);
        IReadOnlyList<Guid>? notifiedUserIds = null;
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(service => service.SendBatchAsync(
                schoolId,
                It.IsAny<IReadOnlyList<Guid>>(),
                "notice_published",
                It.Is<string>(title => title.Contains("Section update")),
                It.IsAny<string?>(),
                noticeId,
                "notice",
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<Guid>, string, string, string?, Guid?, string?, CancellationToken>(
                (_, userIds, _, _, _, _, _, _) => notifiedUserIds = userIds)
            .Returns(Task.CompletedTask);

        var handler = new PublishNoticeCommandHandler(
            context,
            currentUser,
            notificationService.Object,
            Mock.Of<IEmailService>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<PublishNoticeCommandHandler>>());

        var response = await handler.Handle(new PublishNoticeCommand(noticeId), CancellationToken.None);

        response.Message.Should().Be("Notice published successfully. It is now immutable.");
        notifiedUserIds.Should().NotBeNull();
        notifiedUserIds.Should().BeEquivalentTo([teacherAId, parentBId]);

        var notice = await context.Notices.SingleAsync(n => n.Id == noticeId);
        notice.IsPublished.Should().BeTrue();
        notice.PublishedAt.Should().NotBeNull();
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NoticeTargeting_{Guid.NewGuid()}")
            .Options;
    }

    private static CurrentUserService CreateCurrentUser(Guid schoolId, string role, Guid? userId = null)
    {
        return new CurrentUserService
        {
            UserId = userId ?? Guid.NewGuid(),
            SchoolId = schoolId,
            Role = role,
            Name = $"{role} User"
        };
    }

    private static ClassEntity CreateClass(Guid id, Guid schoolId, string name, string section, string academicYear)
    {
        return new ClassEntity
        {
            Id = id,
            SchoolId = schoolId,
            Name = name,
            Section = section,
            AcademicYear = academicYear,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static StudentEntity CreateStudent(Guid id, Guid schoolId, Guid classId, string rollNumber, string name)
    {
        return new StudentEntity
        {
            Id = id,
            SchoolId = schoolId,
            ClassId = classId,
            RollNumber = rollNumber,
            Name = name,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static UserEntity CreateUser(Guid id, Guid schoolId, string role, string phone, string name, string email)
    {
        return new UserEntity
        {
            Id = id,
            SchoolId = schoolId,
            Phone = phone,
            Email = email,
            Name = name,
            Role = role,
            PasswordHash = role == "Teacher" || role == "Admin" ? "hashed-password" : null,
            PinHash = role == "Parent" ? "hashed-pin" : null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ParentStudentLinkEntity CreateParentLink(Guid schoolId, Guid parentId, Guid studentId, string relationship)
    {
        return new ParentStudentLinkEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            ParentId = parentId,
            StudentId = studentId,
            Relationship = relationship,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static NoticeTargetClassEntity CreateNoticeTarget(Guid noticeId, Guid classId, Guid schoolId)
    {
        return new NoticeTargetClassEntity
        {
            NoticeId = noticeId,
            ClassId = classId,
            SchoolId = schoolId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<UserEntity>? users = null,
        IEnumerable<StudentEntity>? students = null,
        IEnumerable<TeacherClassAssignmentEntity>? assignments = null,
        IEnumerable<ParentStudentLinkEntity>? parentLinks = null,
        IEnumerable<NoticeEntity>? notices = null,
        IEnumerable<NoticeTargetClassEntity>? noticeTargets = null)
    {
        await using var context = new AppDbContext(options);

        context.Schools.Add(new SchoolEntity
        {
            Id = schoolId,
            Name = "Test School",
            Code = $"SCH-{schoolId.ToString()[..6]}",
            Address = "Test Address",
            ContactPhone = "09999999999",
            ContactEmail = "school@test.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        if (classes != null)
        {
            context.Classes.AddRange(classes);
        }

        if (users != null)
        {
            context.Users.AddRange(users);
        }

        if (students != null)
        {
            context.Students.AddRange(students);
        }

        if (assignments != null)
        {
            context.TeacherClassAssignments.AddRange(assignments);
        }

        if (parentLinks != null)
        {
            context.ParentStudentLinks.AddRange(parentLinks);
        }

        if (notices != null)
        {
            context.Notices.AddRange(notices);
        }

        if (noticeTargets != null)
        {
            context.NoticeTargetClasses.AddRange(noticeTargets);
        }

        await context.SaveChangesAsync();
    }
}
