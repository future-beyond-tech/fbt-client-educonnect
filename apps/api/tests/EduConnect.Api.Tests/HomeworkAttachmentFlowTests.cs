using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments.DeleteAttachment;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;
using EduConnect.Api.Features.Attachments.RequestUploadUrlV2;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class HomeworkAttachmentFlowTests
{
    [Fact]
    public void RequestUploadUrlV2Validator_AllowsWordDocumentsForHomework()
    {
        var validator = CreateUploadValidator();

        var result = validator.Validate(
            new RequestUploadUrlV2Command(
                "lesson-plan.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                1024,
                "homework"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RequestUploadUrlV2Validator_RejectsWordDocumentsForNotices()
    {
        var validator = CreateUploadValidator();

        var result = validator.Validate(
            new RequestUploadUrlV2Command(
                "staff-circular.doc",
                "application/msword",
                1024,
                "notice"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.ErrorMessage == "This file type is not allowed for the selected entity.");
    }

    [Fact]
    public async Task RequestUploadUrlV2Handler_PersistsPreparedEntityTypeWithoutEntityId()
    {
        var schoolId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000040", "Teacher User", "teacher@test.com")
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var storageService = new Mock<IStorageService>(MockBehavior.Strict);
        storageService
            .Setup(service => service.GeneratePresignedUploadUrlAsync(
                It.IsAny<string>(),
                "application/pdf",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://upload.example.com");

        var handler = new RequestUploadUrlV2CommandHandler(
            context,
            currentUser,
            storageService.Object,
            Options.Create(new StorageOptions()),
            Mock.Of<ILogger<RequestUploadUrlV2CommandHandler>>());

        var response = await handler.Handle(
            new RequestUploadUrlV2Command(
                "worksheet.pdf",
                "application/pdf",
                2048,
                "homework"),
            CancellationToken.None);

        response.UploadUrl.Should().Be("https://upload.example.com");

        var attachment = await context.Attachments.FirstAsync();
        attachment.EntityId.Should().BeNull();
        attachment.EntityType.Should().Be("homework");
        attachment.FileName.Should().Be("worksheet.pdf");

        storageService.VerifyAll();
    }

    [Fact]
    public async Task GetAttachmentsForEntity_ParentCannotViewDraftHomeworkAttachments()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Parent", parentId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classId, schoolId, "5", "A")
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000041", "Teacher", "teacher@test.com"),
                CreateParentUser(parentId, schoolId, "9000000042", "Parent", "parent@test.com")
            ],
            students:
            [
                new StudentEntity
                {
                    Id = studentId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    RollNumber = "R001",
                    Name = "Aarav"
                }
            ],
            parentLinks:
            [
                new ParentStudentLinkEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ParentId = parentId,
                    StudentId = studentId,
                    Relationship = "parent"
                }
            ],
            homeworks:
            [
                CreateHomework(homeworkId, schoolId, classId, teacherId, "Draft")
            ],
            attachments:
            [
                new AttachmentEntity
                {
                    Id = attachmentId,
                    SchoolId = schoolId,
                    EntityId = homeworkId,
                    EntityType = "homework",
                    StorageKey = "school/homework/file.pdf",
                    FileName = "file.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    UploadedById = teacherId,
                    UploadedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var storageService = new Mock<IStorageService>(MockBehavior.Strict);
        var handler = new GetAttachmentsForEntityQueryHandler(
            context,
            currentUser,
            storageService.Object);

        var act = () => handler.Handle(
            new GetAttachmentsForEntityQuery(homeworkId, "homework"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not have access to these homework attachments.");

        storageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAttachmentsForEntity_ParentCanViewPublishedHomeworkAttachments()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Parent", parentId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classId, schoolId, "6", "B")
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000043", "Teacher", "teacher@test.com"),
                CreateParentUser(parentId, schoolId, "9000000044", "Parent", "parent@test.com")
            ],
            students:
            [
                new StudentEntity
                {
                    Id = studentId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    RollNumber = "R002",
                    Name = "Ira"
                }
            ],
            parentLinks:
            [
                new ParentStudentLinkEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ParentId = parentId,
                    StudentId = studentId,
                    Relationship = "mother"
                }
            ],
            homeworks:
            [
                CreateHomework(homeworkId, schoolId, classId, teacherId, "Published")
            ],
            attachments:
            [
                new AttachmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    EntityId = homeworkId,
                    EntityType = "homework",
                    StorageKey = "school/homework/file.docx",
                    FileName = "file.docx",
                    ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    SizeBytes = 4096,
                    UploadedById = teacherId,
                    UploadedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var storageService = new Mock<IStorageService>(MockBehavior.Strict);
        storageService
            .Setup(service => service.GeneratePresignedDownloadUrlAsync(
                "school/homework/file.docx",
                It.IsAny<TimeSpan>(),
                "file.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://download.example.com/file.docx");

        var handler = new GetAttachmentsForEntityQueryHandler(
            context,
            currentUser,
            storageService.Object);

        var result = await handler.Handle(
            new GetAttachmentsForEntityQuery(homeworkId, "homework"),
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].DownloadUrl.Should().Be("https://download.example.com/file.docx");

        storageService.VerifyAll();
    }

    [Fact]
    public async Task DeleteAttachment_TeacherCannotDeleteAttachmentFromPublishedHomework()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classId, schoolId, "7", "C")
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000045", "Teacher", "teacher@test.com")
            ],
            homeworks:
            [
                CreateHomework(homeworkId, schoolId, classId, teacherId, "Published")
            ],
            attachments:
            [
                new AttachmentEntity
                {
                    Id = attachmentId,
                    SchoolId = schoolId,
                    EntityId = homeworkId,
                    EntityType = "homework",
                    StorageKey = "school/homework/file.pdf",
                    FileName = "file.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    UploadedById = teacherId,
                    UploadedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var storageService = new Mock<IStorageService>(MockBehavior.Strict);
        var handler = new DeleteAttachmentCommandHandler(
            context,
            currentUser,
            storageService.Object,
            Mock.Of<ILogger<DeleteAttachmentCommandHandler>>());

        var act = () => handler.Handle(new DeleteAttachmentCommand(attachmentId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Attachments can only be deleted while homework is editable.");

        storageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAttachment_TeacherCanDeleteAttachmentFromDraftHomework()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                CreateClass(classId, schoolId, "8", "A")
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000046", "Teacher", "teacher@test.com")
            ],
            homeworks:
            [
                CreateHomework(homeworkId, schoolId, classId, teacherId, "Draft")
            ],
            attachments:
            [
                new AttachmentEntity
                {
                    Id = attachmentId,
                    SchoolId = schoolId,
                    EntityId = homeworkId,
                    EntityType = "homework",
                    StorageKey = "school/homework/file.pdf",
                    FileName = "file.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    UploadedById = teacherId,
                    UploadedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var storageService = new Mock<IStorageService>(MockBehavior.Strict);
        storageService
            .Setup(service => service.DeleteObjectAsync("school/homework/file.pdf", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new DeleteAttachmentCommandHandler(
            context,
            currentUser,
            storageService.Object,
            Mock.Of<ILogger<DeleteAttachmentCommandHandler>>());

        var response = await handler.Handle(
            new DeleteAttachmentCommand(attachmentId),
            CancellationToken.None);

        response.Message.Should().Be("Attachment deleted successfully.");
        (await context.Attachments.AnyAsync(a => a.Id == attachmentId)).Should().BeFalse();
        storageService.VerifyAll();
    }

    private static RequestUploadUrlV2CommandValidator CreateUploadValidator()
    {
        return new RequestUploadUrlV2CommandValidator(
            Options.Create(new StorageOptions()));
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"HomeworkAttachment_{Guid.NewGuid()}")
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

    private static ClassEntity CreateClass(Guid classId, Guid schoolId, string name, string section)
    {
        return new ClassEntity
        {
            Id = classId,
            SchoolId = schoolId,
            Name = name,
            Section = section,
            AcademicYear = "2026-27"
        };
    }

    private static HomeworkEntity CreateHomework(
        Guid homeworkId,
        Guid schoolId,
        Guid classId,
        Guid teacherId,
        string status)
    {
        return new HomeworkEntity
        {
            Id = homeworkId,
            SchoolId = schoolId,
            ClassId = classId,
            Subject = "Science",
            Title = "Homework",
            Description = "Practice work",
            AssignedById = teacherId,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = status,
            PublishedAt = status == "Published" ? DateTimeOffset.UtcNow : null,
            SubmittedAt = status == "PendingApproval" ? DateTimeOffset.UtcNow : null,
            IsEditable = status == "Draft" || status == "Rejected"
        };
    }

    private static UserEntity CreateTeacherUser(Guid id, Guid schoolId, string phone, string name, string email)
    {
        return new UserEntity
        {
            Id = id,
            SchoolId = schoolId,
            Phone = phone,
            Email = email,
            Name = name,
            Role = "Teacher",
            PasswordHash = "hashed",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static UserEntity CreateParentUser(Guid id, Guid schoolId, string phone, string name, string email)
    {
        return new UserEntity
        {
            Id = id,
            SchoolId = schoolId,
            Phone = phone,
            Email = email,
            Name = name,
            Role = "Parent",
            PinHash = "hashed",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<UserEntity>? users = null,
        IEnumerable<StudentEntity>? students = null,
        IEnumerable<ParentStudentLinkEntity>? parentLinks = null,
        IEnumerable<HomeworkEntity>? homeworks = null,
        IEnumerable<AttachmentEntity>? attachments = null)
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

        if (parentLinks != null)
        {
            context.ParentStudentLinks.AddRange(parentLinks);
        }

        if (homeworks != null)
        {
            context.Homeworks.AddRange(homeworks);
        }

        if (attachments != null)
        {
            context.Attachments.AddRange(attachments);
        }

        await context.SaveChangesAsync();
    }
}
