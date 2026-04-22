using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using EduConnect.Api.Infrastructure.Services.Scanning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Phase 6.1 — operator notifications when an upload is blocked.
/// </summary>
public class AttachmentBlockedNotifierTests
{
    [Fact]
    public async Task NotifyAsync_dispatches_in_app_to_every_active_admin_in_tenant_for_Infected()
    {
        var (db, attachment, schoolId, adminA, adminB, adminInOtherTenant, inactiveAdmin) =
            await SeedTenantWithAdmins();

        var notification = new RecordingNotificationService();
        var email = new Mock<IEmailService>();
        var notifier = new AttachmentBlockedNotifier(
            db, notification, email.Object, NullLogger<AttachmentBlockedNotifier>.Instance);

        attachment.ThreatName = "Eicar-Test-Signature";
        await notifier.NotifyAsync(AttachmentBlockedKind.Infected, attachment);

        notification.Calls.Should().ContainSingle();
        var call = notification.Calls[0];
        call.SchoolId.Should().Be(schoolId);
        call.UserIds.Should().BeEquivalentTo(new[] { adminA, adminB },
            "the inactive admin and the cross-tenant admin must not receive the alert");
        call.Type.Should().Be("attachment_infected");
        call.Title.Should().Contain(attachment.FileName);
        call.Body.Should().Contain("Eicar-Test-Signature");
        call.EntityId.Should().Be(attachment.Id);
        call.EntityType.Should().Be("attachment");

        // Other tenant's admin must not have been considered at all.
        adminInOtherTenant.Should().NotBe(Guid.Empty);
        inactiveAdmin.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task NotifyAsync_uses_attachment_scan_failed_type_for_ScanFailed()
    {
        var (db, attachment, _, _, _, _, _) = await SeedTenantWithAdmins();

        var notification = new RecordingNotificationService();
        var notifier = new AttachmentBlockedNotifier(
            db, notification, Mock.Of<IEmailService>(), NullLogger<AttachmentBlockedNotifier>.Instance);

        attachment.ThreatName = "scanner_unreachable";
        await notifier.NotifyAsync(AttachmentBlockedKind.ScanFailed, attachment);

        notification.Calls.Single().Type.Should().Be("attachment_scan_failed");
        notification.Calls.Single().Body.Should().Contain("scanner_unreachable");
    }

    [Fact]
    public async Task NotifyAsync_emails_each_admin_with_an_email_address()
    {
        var (db, attachment, _, _, _, _, _) = await SeedTenantWithAdmins();

        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var notifier = new AttachmentBlockedNotifier(
            db, new RecordingNotificationService(), email.Object,
            NullLogger<AttachmentBlockedNotifier>.Instance);

        await notifier.NotifyAsync(AttachmentBlockedKind.Infected, attachment);

        // adminA has an email; adminB doesn't (see SeedTenantWithAdmins).
        email.Verify(e => e.SendEmailAsync(
            "admin-a@test.com",
            It.Is<string>(s => s.Contains(attachment.FileName)),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        email.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_swallows_notification_failure_so_it_never_undoes_a_DB_write()
    {
        var (db, attachment, _, _, _, _, _) = await SeedTenantWithAdmins();

        var notification = new ThrowingNotificationService();
        var notifier = new AttachmentBlockedNotifier(
            db, notification, Mock.Of<IEmailService>(),
            NullLogger<AttachmentBlockedNotifier>.Instance);

        // Must not throw — primary write has already been saved by caller.
        var act = () => notifier.NotifyAsync(AttachmentBlockedKind.Infected, attachment);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyAsync_does_nothing_when_tenant_has_no_admins()
    {
        var schoolId = Guid.NewGuid();
        var options = NewOptions();
        await using (var ctx = new AppDbContext(options))
        {
            ctx.Schools.Add(new SchoolEntity
            {
                Id = schoolId, Name = "S", Code = "S",
                Address = "", ContactPhone = "", ContactEmail = "",
            });
            await ctx.SaveChangesAsync();
        }

        var notification = new RecordingNotificationService();
        await using var db = new AppDbContext(options);
        var notifier = new AttachmentBlockedNotifier(
            db, notification, Mock.Of<IEmailService>(),
            NullLogger<AttachmentBlockedNotifier>.Instance);

        var attachment = new AttachmentEntity
        {
            Id = Guid.NewGuid(), SchoolId = schoolId, FileName = "f.pdf",
            ContentType = "application/pdf", SizeBytes = 1, StorageKey = "k",
            UploadedById = Guid.NewGuid(),
        };

        await notifier.NotifyAsync(AttachmentBlockedKind.Infected, attachment);

        notification.Calls.Should().BeEmpty();
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> NewOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NotifierTests_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(
        AppDbContext Db,
        AttachmentEntity Attachment,
        Guid SchoolId,
        Guid AdminA,
        Guid AdminB,
        Guid AdminInOtherTenant,
        Guid InactiveAdmin)> SeedTenantWithAdmins()
    {
        var schoolId = Guid.NewGuid();
        var otherSchoolId = Guid.NewGuid();
        var adminA = Guid.NewGuid();
        var adminB = Guid.NewGuid();
        var adminInOtherTenant = Guid.NewGuid();
        var inactiveAdmin = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var options = NewOptions();
        await using (var seed = new AppDbContext(options))
        {
            seed.Schools.AddRange(
                new SchoolEntity { Id = schoolId,      Name = "Primary", Code = "A",
                    Address = "", ContactPhone = "", ContactEmail = "" },
                new SchoolEntity { Id = otherSchoolId, Name = "Other",   Code = "B",
                    Address = "", ContactPhone = "", ContactEmail = "" });

            seed.Users.AddRange(
                new UserEntity { Id = adminA,             SchoolId = schoolId,      Phone = "0900000001",
                    Email = "admin-a@test.com", Name = "Admin A", Role = "Admin", IsActive = true },
                new UserEntity { Id = adminB,             SchoolId = schoolId,      Phone = "0900000002",
                    Email = null,                Name = "Admin B", Role = "Admin", IsActive = true },
                new UserEntity { Id = inactiveAdmin,      SchoolId = schoolId,      Phone = "0900000003",
                    Email = "inactive@test.com", Name = "Old Admin", Role = "Admin", IsActive = false },
                new UserEntity { Id = adminInOtherTenant, SchoolId = otherSchoolId, Phone = "0900000004",
                    Email = "other@test.com",    Name = "Cross-tenant Admin", Role = "Admin", IsActive = true },
                new UserEntity { Id = Guid.NewGuid(),     SchoolId = schoolId,      Phone = "0900000005",
                    Email = "teach@test.com",    Name = "Teacher",  Role = "Teacher", IsActive = true });

            seed.Attachments.Add(new AttachmentEntity
            {
                Id = attachmentId, SchoolId = schoolId,
                EntityType = "homework", EntityId = Guid.NewGuid(),
                FileName = "lesson.pdf", ContentType = "application/pdf",
                SizeBytes = 1024, StorageKey = $"{schoolId}/homework/{attachmentId}",
                UploadedById = Guid.NewGuid(),
                UploadedAt = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Infected,
                ThreatName = "Eicar-Test-Signature",
            });
            await seed.SaveChangesAsync();
        }

        var db = new AppDbContext(options);
        var attachment = await db.Attachments.SingleAsync(a => a.Id == attachmentId);
        return (db, attachment, schoolId, adminA, adminB, adminInOtherTenant, inactiveAdmin);
    }

    private sealed record SendBatchCall(
        Guid SchoolId,
        IReadOnlyList<Guid> UserIds,
        string Type,
        string Title,
        string? Body,
        Guid? EntityId,
        string? EntityType);

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<SendBatchCall> Calls { get; } = new();

        public Task SendAsync(
            Guid schoolId, Guid userId, string type, string title, string? body,
            Guid? entityId, string? entityType, CancellationToken cancellationToken = default)
        {
            Calls.Add(new SendBatchCall(schoolId, new[] { userId }, type, title, body, entityId, entityType));
            return Task.CompletedTask;
        }

        public Task SendBatchAsync(
            Guid schoolId, IReadOnlyList<Guid> userIds, string type, string title, string? body,
            Guid? entityId, string? entityType, CancellationToken cancellationToken = default)
        {
            Calls.Add(new SendBatchCall(schoolId, userIds.ToList(), type, title, body, entityId, entityType));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task SendAsync(
            Guid schoolId, Guid userId, string type, string title, string? body,
            Guid? entityId, string? entityType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated DB outage");

        public Task SendBatchAsync(
            Guid schoolId, IReadOnlyList<Guid> userIds, string type, string title, string? body,
            Guid? entityId, string? entityType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated DB outage");
    }
}
