using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Closes the parent-notice access gap: HomeworkSubmissionAttachmentAclTests
/// has equivalent coverage for the sibling entity, but the notice branch in
/// GetAttachmentsForEntityQueryHandler.EnsureNoticeAccessAsync had no
/// end-to-end parent ACL test. Pins the audience + publish + expiry +
/// tenant + status matrix so future edits don't silently leak or hide
/// attachments.
/// </summary>
public class NoticeAttachmentAclTests
{
    [Fact]
    public async Task Parent_can_view_school_wide_published_notice_attachment()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var attachments = await RunHandler(f.Options, parent, f.SchoolWideNoticeId);

        attachments.Should().ContainSingle();
        attachments[0].FileName.Should().Be("schoolwide.pdf");
    }

    [Fact]
    public async Task Parent_can_view_attachment_on_notice_targeting_their_child_class()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var attachments = await RunHandler(f.Options, parent, f.TargetedNoticeId);

        attachments.Should().ContainSingle();
        attachments[0].FileName.Should().Be("targeted.pdf");
    }

    [Fact]
    public async Task Parent_outside_targeted_classes_is_forbidden()
    {
        var f = await Seed();

        var otherParent = CurrentUser(f.SchoolId, "Parent", f.OtherParentId);
        var act = () => RunHandler(f.Options, otherParent, f.TargetedNoticeId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not have access to these notice attachments.");
    }

    [Fact]
    public async Task Parent_cannot_view_unpublished_draft_notice_attachments()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var act = () => RunHandler(f.Options, parent, f.DraftNoticeId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not have access to these notice attachments.");
    }

    [Fact]
    public async Task Parent_cannot_view_expired_notice_attachments()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var act = () => RunHandler(f.Options, parent, f.ExpiredNoticeId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not have access to these notice attachments.");
    }

    [Fact]
    public async Task Parent_in_other_tenant_gets_not_found_for_cross_tenant_notice()
    {
        var f = await Seed();

        var crossTenantParent = CurrentUser(f.OtherSchoolId, "Parent", f.OtherSchoolParentId);
        var act = () => RunHandler(f.Options, crossTenantParent, f.SchoolWideNoticeId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ScanFailed_attachment_is_hidden_from_parent_even_on_visible_notice()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var attachments = await RunHandler(f.Options, parent, f.NoticeWithScanFailedId);

        attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_attachment_is_visible_to_parent_on_visible_notice()
    {
        var f = await Seed();

        var parent = CurrentUser(f.SchoolId, "Parent", f.ParentId);
        var attachments = await RunHandler(f.Options, parent, f.SchoolWideNoticeId);

        attachments.Should().ContainSingle(a => a.Status == AttachmentStatus.Available);
    }

    private static async Task<List<AttachmentDto>> RunHandler(
        DbContextOptions<AppDbContext> options,
        CurrentUserService currentUser,
        Guid noticeId)
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
            storage.Object,
            Options.Create(new StorageOptions()));

        return await handler.Handle(
            new GetAttachmentsForEntityQuery(noticeId, "notice"),
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
        var otherSchoolId = Guid.NewGuid();
        var targetClassId = Guid.NewGuid();
        var otherClassId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var otherParentId = Guid.NewGuid();
        var otherSchoolParentId = Guid.NewGuid();
        var parentStudentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();

        var schoolWideNoticeId = Guid.NewGuid();
        var targetedNoticeId = Guid.NewGuid();
        var draftNoticeId = Guid.NewGuid();
        var expiredNoticeId = Guid.NewGuid();
        var noticeWithScanFailedId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NoticeAcl_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var ctx = new AppDbContext(options);
        ctx.Schools.AddRange(
            new SchoolEntity
            {
                Id = schoolId,
                Name = "Test School",
                Code = "TST",
                Address = "",
                ContactPhone = "",
                ContactEmail = "",
            },
            new SchoolEntity
            {
                Id = otherSchoolId,
                Name = "Other School",
                Code = "OTH",
                Address = "",
                ContactPhone = "",
                ContactEmail = "",
            });

        ctx.Classes.AddRange(
            new ClassEntity
            {
                Id = targetClassId,
                SchoolId = schoolId,
                Name = "7",
                Section = "A",
                AcademicYear = "2026",
            },
            new ClassEntity
            {
                Id = otherClassId,
                SchoolId = schoolId,
                Name = "7",
                Section = "B",
                AcademicYear = "2026",
            });

        ctx.Users.AddRange(
            new UserEntity { Id = adminId,              SchoolId = schoolId,      Phone = "09000000001", Name = "Admin",              Role = "Admin" },
            new UserEntity { Id = parentId,             SchoolId = schoolId,      Phone = "09000000002", Name = "Parent",             Role = "Parent" },
            new UserEntity { Id = otherParentId,        SchoolId = schoolId,      Phone = "09000000003", Name = "Other Parent",       Role = "Parent" },
            new UserEntity { Id = otherSchoolParentId,  SchoolId = otherSchoolId, Phone = "09000000004", Name = "OtherSchool Parent", Role = "Parent" });

        ctx.Students.AddRange(
            new StudentEntity { Id = parentStudentId, SchoolId = schoolId, ClassId = targetClassId, Name = "Child",       RollNumber = "001", IsActive = true },
            new StudentEntity { Id = otherStudentId,  SchoolId = schoolId, ClassId = otherClassId,  Name = "Other Child", RollNumber = "002", IsActive = true });

        ctx.ParentStudentLinks.AddRange(
            new ParentStudentLinkEntity { Id = Guid.NewGuid(), SchoolId = schoolId, ParentId = parentId,      StudentId = parentStudentId, Relationship = "parent" },
            new ParentStudentLinkEntity { Id = Guid.NewGuid(), SchoolId = schoolId, ParentId = otherParentId, StudentId = otherStudentId,  Relationship = "parent" });

        var now = DateTimeOffset.UtcNow;

        ctx.Notices.AddRange(
            new NoticeEntity
            {
                Id = schoolWideNoticeId,
                SchoolId = schoolId,
                Title = "School wide",
                Body = "All hands.",
                TargetAudience = "All",
                PublishedById = adminId,
                IsPublished = true,
                PublishedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new NoticeEntity
            {
                Id = targetedNoticeId,
                SchoolId = schoolId,
                Title = "Targeted",
                Body = "Class only.",
                TargetAudience = "Class",
                PublishedById = adminId,
                IsPublished = true,
                PublishedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new NoticeEntity
            {
                Id = draftNoticeId,
                SchoolId = schoolId,
                Title = "Draft",
                Body = "Not yet.",
                TargetAudience = "All",
                PublishedById = adminId,
                IsPublished = false,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new NoticeEntity
            {
                Id = expiredNoticeId,
                SchoolId = schoolId,
                Title = "Expired",
                Body = "Stale.",
                TargetAudience = "All",
                PublishedById = adminId,
                IsPublished = true,
                PublishedAt = now.AddDays(-2),
                ExpiresAt = now.AddDays(-1),
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2),
            },
            new NoticeEntity
            {
                Id = noticeWithScanFailedId,
                SchoolId = schoolId,
                Title = "Has scan-failed attachment",
                Body = "Only the ScanFailed row.",
                TargetAudience = "All",
                PublishedById = adminId,
                IsPublished = true,
                PublishedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });

        ctx.NoticeTargetClasses.Add(new NoticeTargetClassEntity
        {
            SchoolId = schoolId,
            NoticeId = targetedNoticeId,
            ClassId = targetClassId,
            CreatedAt = now,
        });

        ctx.Attachments.AddRange(
            new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                EntityId = schoolWideNoticeId,
                EntityType = "notice",
                StorageKey = $"{schoolId}/notice/{schoolWideNoticeId}/schoolwide.pdf",
                FileName = "schoolwide.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                UploadedById = adminId,
                UploadedAt = now,
                Status = AttachmentStatus.Available,
            },
            new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                EntityId = targetedNoticeId,
                EntityType = "notice",
                StorageKey = $"{schoolId}/notice/{targetedNoticeId}/targeted.pdf",
                FileName = "targeted.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                UploadedById = adminId,
                UploadedAt = now,
                Status = AttachmentStatus.Available,
            },
            new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                EntityId = draftNoticeId,
                EntityType = "notice",
                StorageKey = $"{schoolId}/notice/{draftNoticeId}/draft.pdf",
                FileName = "draft.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                UploadedById = adminId,
                UploadedAt = now,
                Status = AttachmentStatus.Available,
            },
            new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                EntityId = expiredNoticeId,
                EntityType = "notice",
                StorageKey = $"{schoolId}/notice/{expiredNoticeId}/expired.pdf",
                FileName = "expired.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                UploadedById = adminId,
                UploadedAt = now.AddDays(-2),
                Status = AttachmentStatus.Available,
            },
            new AttachmentEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                EntityId = noticeWithScanFailedId,
                EntityType = "notice",
                StorageKey = $"{schoolId}/notice/{noticeWithScanFailedId}/broken.pdf",
                FileName = "broken.pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024,
                UploadedById = adminId,
                UploadedAt = now,
                Status = AttachmentStatus.ScanFailed,
                ThreatName = "some-engine-error",
            });

        await ctx.SaveChangesAsync();

        return new Fixture(
            options,
            schoolId,
            otherSchoolId,
            parentId,
            otherParentId,
            otherSchoolParentId,
            schoolWideNoticeId,
            targetedNoticeId,
            draftNoticeId,
            expiredNoticeId,
            noticeWithScanFailedId);
    }

    private sealed record Fixture(
        DbContextOptions<AppDbContext> Options,
        Guid SchoolId,
        Guid OtherSchoolId,
        Guid ParentId,
        Guid OtherParentId,
        Guid OtherSchoolParentId,
        Guid SchoolWideNoticeId,
        Guid TargetedNoticeId,
        Guid DraftNoticeId,
        Guid ExpiredNoticeId,
        Guid NoticeWithScanFailedId);
}
