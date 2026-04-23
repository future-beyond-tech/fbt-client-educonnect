using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Notices.GetNoticeById;
using EduConnect.Api.Features.Notices.GetNotices;
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

// Covers GET /api/notices/{id} (the preview page's data source) plus the
// preview-page publish semantics: publish must still succeed when some
// attachments are Pending or ScanFailed (the UI warns but does not block).
public class NoticePreviewFlowTests
{
    [Fact]
    public async Task GetNoticeById_AsCreatorAdmin_ReturnsDraftWithFullCapabilities()
    {
        var schoolId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var noticeId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin", adminId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes: [CreateClass(classId, schoolId, "5", "A", "2026-27")],
            notices:
            [
                new NoticeEntity
                {
                    Id = noticeId,
                    SchoolId = schoolId,
                    Title = "Upcoming events",
                    Body = "Preview body.",
                    TargetAudience = "Class",
                    PublishedById = adminId,
                    IsPublished = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ],
            noticeTargets:
            [
                new NoticeTargetClassEntity
                {
                    NoticeId = noticeId,
                    ClassId = classId,
                    SchoolId = schoolId,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetNoticeByIdQueryHandler(context, currentUser);

        var result = await handler.Handle(new GetNoticeByIdQuery(noticeId), CancellationToken.None);

        result.NoticeId.Should().Be(noticeId);
        result.IsPublished.Should().BeFalse();
        result.TargetClasses.Should().ContainSingle(t => t.ClassId == classId);
        result.Capabilities.CanEditDraft.Should().BeTrue();
        result.Capabilities.CanManageDraftAttachments.Should().BeTrue();
        result.Capabilities.CanPreviewDraft.Should().BeTrue();
        result.Capabilities.CanPublishDraft.Should().BeTrue();
    }

    [Fact]
    public async Task GetNoticeById_AsNonCreatorAdmin_CannotEditButCanPublish()
    {
        var schoolId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherAdminId = Guid.NewGuid();
        var noticeId = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            notices:
            [
                new NoticeEntity
                {
                    Id = noticeId,
                    SchoolId = schoolId,
                    Title = "Other admin's draft",
                    Body = "Body",
                    TargetAudience = "All",
                    PublishedById = creatorId,
                    IsPublished = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Admin", otherAdminId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetNoticeByIdQueryHandler(context, currentUser);

        var result = await handler.Handle(new GetNoticeByIdQuery(noticeId), CancellationToken.None);

        result.Capabilities.CanEditDraft.Should().BeFalse();
        result.Capabilities.CanManageDraftAttachments.Should().BeTrue();
        result.Capabilities.CanPreviewDraft.Should().BeTrue();
        result.Capabilities.CanPublishDraft.Should().BeTrue();
    }

    [Fact]
    public async Task GetNoticeById_PublishedNotice_HasAllDraftCapabilitiesFalse()
    {
        var schoolId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var noticeId = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            notices:
            [
                new NoticeEntity
                {
                    Id = noticeId,
                    SchoolId = schoolId,
                    Title = "Published",
                    Body = "Body",
                    TargetAudience = "All",
                    PublishedById = adminId,
                    IsPublished = true,
                    PublishedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Admin", adminId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetNoticeByIdQueryHandler(context, currentUser);

        var result = await handler.Handle(new GetNoticeByIdQuery(noticeId), CancellationToken.None);

        result.IsPublished.Should().BeTrue();
        result.Capabilities.CanEditDraft.Should().BeFalse();
        result.Capabilities.CanManageDraftAttachments.Should().BeFalse();
        result.Capabilities.CanPreviewDraft.Should().BeFalse();
        result.Capabilities.CanPublishDraft.Should().BeFalse();
    }

    [Fact]
    public async Task GetNoticeById_AsParent_WithUnrelatedDraft_ReturnsNotFound()
    {
        var schoolId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var noticeId = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            notices:
            [
                new NoticeEntity
                {
                    Id = noticeId,
                    SchoolId = schoolId,
                    Title = "Draft",
                    Body = "Body",
                    TargetAudience = "All",
                    PublishedById = adminId,
                    IsPublished = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Parent", parentId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetNoticeByIdQueryHandler(context, currentUser);

        var act = () => handler.Handle(new GetNoticeByIdQuery(noticeId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetNoticeById_UnknownNotice_ReturnsNotFound()
    {
        var schoolId = Guid.NewGuid();
        var options = CreateOptions();
        await SeedAsync(options, schoolId);

        var currentUser = CreateCurrentUser(schoolId, "Admin");
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetNoticeByIdQueryHandler(context, currentUser);

        var act = () => handler.Handle(new GetNoticeByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishNotice_WithPendingOrScanFailedAttachments_SucceedsAndMarksPublished()
    {
        var schoolId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var noticeId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin", adminId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            notices:
            [
                new NoticeEntity
                {
                    Id = noticeId,
                    SchoolId = schoolId,
                    Title = "Has unresolved files",
                    Body = "Some attachments pending.",
                    TargetAudience = "All",
                    PublishedById = adminId,
                    IsPublished = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ],
            attachments:
            [
                new AttachmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    UploadedById = adminId,
                    FileName = "pending.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024,
                    StorageKey = $"schools/{schoolId}/notices/{noticeId}/pending.pdf",
                    EntityId = noticeId,
                    EntityType = "notice",
                    Status = "Pending",
                    UploadedAt = DateTimeOffset.UtcNow
                },
                new AttachmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    UploadedById = adminId,
                    FileName = "scan-failed.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 2048,
                    StorageKey = $"schools/{schoolId}/notices/{noticeId}/scan-failed.pdf",
                    EntityId = noticeId,
                    EntityType = "notice",
                    Status = "ScanFailed",
                    UploadedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.SendBatchAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new PublishNoticeCommandHandler(
            context,
            currentUser,
            notifications.Object,
            Mock.Of<IEmailService>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<PublishNoticeCommandHandler>>());

        var response = await handler.Handle(
            new PublishNoticeCommand(noticeId),
            CancellationToken.None);

        response.Message.Should().Be("Notice published successfully. It is now immutable.");

        var reloaded = await context.Notices.SingleAsync(n => n.Id == noticeId);
        reloaded.IsPublished.Should().BeTrue();
        reloaded.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNotices_ReturnsCapabilitiesOnEveryRow()
    {
        var schoolId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var publishedId = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            notices:
            [
                new NoticeEntity
                {
                    Id = draftId,
                    SchoolId = schoolId,
                    Title = "Draft",
                    Body = "D",
                    TargetAudience = "All",
                    PublishedById = adminId,
                    IsPublished = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new NoticeEntity
                {
                    Id = publishedId,
                    SchoolId = schoolId,
                    Title = "Published",
                    Body = "P",
                    TargetAudience = "All",
                    PublishedById = adminId,
                    IsPublished = true,
                    PublishedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Admin", adminId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetNoticesQueryHandler(context, currentUser);

        var results = await handler.Handle(new GetNoticesQuery(), CancellationToken.None);

        results.Should().HaveCount(2);
        var draft = results.Single(r => r.NoticeId == draftId);
        draft.Capabilities.CanPublishDraft.Should().BeTrue();
        draft.Capabilities.CanEditDraft.Should().BeTrue();

        var published = results.Single(r => r.NoticeId == publishedId);
        published.Capabilities.CanPublishDraft.Should().BeFalse();
        published.Capabilities.CanEditDraft.Should().BeFalse();
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NoticePreview_{Guid.NewGuid()}")
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

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<NoticeEntity>? notices = null,
        IEnumerable<NoticeTargetClassEntity>? noticeTargets = null,
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

        if (classes != null) context.Classes.AddRange(classes);
        if (notices != null) context.Notices.AddRange(notices);
        if (noticeTargets != null) context.NoticeTargetClasses.AddRange(noticeTargets);
        if (attachments != null) context.Attachments.AddRange(attachments);

        await context.SaveChangesAsync();
    }
}
