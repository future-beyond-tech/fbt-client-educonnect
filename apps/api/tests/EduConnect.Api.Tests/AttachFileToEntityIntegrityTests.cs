using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments.AttachFileToEntity;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using EduConnect.Api.Infrastructure.Services.Scanning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Phase 2.1 + 2.3 — the attach endpoint is the final choke-point before an
/// attachment row is linked to its entity. It must NOT trust the client's
/// declared size / content-type / rule compliance; it must re-check against
/// storage (HeadObject) and AttachmentFeatureRules. These tests pin that
/// contract.
/// </summary>
public class AttachFileToEntityIntegrityTests
{
    [Fact]
    public async Task Attach_fails_when_no_object_exists_in_storage_for_the_key()
    {
        var f = await SeedAsync();
        var storage = StorageReturning(metadata: null);

        var act = () => RunHandler(f, storage);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not uploaded*");
    }

    [Fact]
    public async Task Attach_rejects_when_stored_size_exceeds_max_file_size()
    {
        var f = await SeedAsync();
        var storage = StorageReturning(new StorageObjectMetadata(
            SizeBytes: 11L * 1024 * 1024,
            ContentType: "application/pdf"));

        var act = () => RunHandler(f, storage);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds*max*size*");
    }

    [Fact]
    public async Task Attach_rejects_when_stored_content_type_differs_from_row()
    {
        var f = await SeedAsync();
        var storage = StorageReturning(new StorageObjectMetadata(
            SizeBytes: 2048,
            ContentType: "application/x-msdownload"));

        var act = () => RunHandler(f, storage);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*content type*");
    }

    [Fact]
    public async Task Attach_reconciles_size_when_stored_size_differs_from_declared()
    {
        var f = await SeedAsync(declaredSize: 2048);
        var storage = StorageReturning(new StorageObjectMetadata(
            SizeBytes: 1536,
            ContentType: "application/pdf"));

        await RunHandler(f, storage);

        await using var ctx = new AppDbContext(f.Options, f.CurrentUser);
        var row = await ctx.Attachments.SingleAsync(a => a.Id == f.AttachmentId);
        row.SizeBytes.Should().Be(1536, "server overwrites declared size with the actual S3 size");
        row.EntityId.Should().Be(f.HomeworkId);
    }

    [Fact]
    public async Task Attach_rejects_when_row_content_type_is_not_allowed_for_entity()
    {
        var f = await SeedAsync(
            declaredContentType: "text/html",
            declaredFileName: "evil.html");

        var storage = StorageReturning(new StorageObjectMetadata(
            SizeBytes: 2048,
            ContentType: "text/html"));

        var act = () => RunHandler(f, storage);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task Attach_rejects_when_row_file_extension_is_not_allowed_for_entity()
    {
        // Extension is .exe, content type pretends to be PDF to pass the
        // content-type check — the extension rule still blocks it.
        var f = await SeedAsync(
            declaredContentType: "application/pdf",
            declaredFileName: "malware.exe");

        var storage = StorageReturning(new StorageObjectMetadata(
            SizeBytes: 2048,
            ContentType: "application/pdf"));

        var act = () => RunHandler(f, storage);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*extension*");
    }

    [Fact]
    public async Task Attach_happy_path_links_row_and_enqueues_scan()
    {
        var f = await SeedAsync();
        var storage = StorageReturning(new StorageObjectMetadata(
            SizeBytes: 2048,
            ContentType: "application/pdf"));

        var queue = new Mock<IAttachmentScanQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await RunHandler(f, storage, queue: queue);

        await using var ctx = new AppDbContext(f.Options, f.CurrentUser);
        var row = await ctx.Attachments.SingleAsync(a => a.Id == f.AttachmentId);
        row.EntityId.Should().Be(f.HomeworkId);
        row.EntityType.Should().Be("homework");
        queue.Verify(q => q.EnqueueAsync(f.AttachmentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Task RunHandler(
        Fixture f,
        Mock<IStorageService> storage,
        Mock<IAttachmentScanQueue>? queue = null)
    {
        var queueMock = queue ?? new Mock<IAttachmentScanQueue>();
        queueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var ctx = new AppDbContext(f.Options, f.CurrentUser);
        var handler = new AttachFileToEntityCommandHandler(
            ctx,
            f.CurrentUser,
            storage.Object,
            queueMock.Object,
            Options.Create(new StorageOptions()),
            NullLogger<AttachFileToEntityCommandHandler>.Instance);

        return handler.Handle(
            new AttachFileToEntityCommand(f.AttachmentId, f.HomeworkId, "homework"),
            CancellationToken.None);
    }

    private static Mock<IStorageService> StorageReturning(StorageObjectMetadata? metadata)
    {
        var storage = new Mock<IStorageService>();
        storage
            .Setup(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        return storage;
    }

    private static async Task<Fixture> SeedAsync(
        int declaredSize = 2048,
        string declaredContentType = "application/pdf",
        string declaredFileName = "worksheet.pdf")
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AttachIntegrity_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var currentUser = new CurrentUserService
        {
            SchoolId = schoolId,
            UserId = teacherId,
            Role = "Teacher",
            Name = "Teacher",
        };

        await using (var ctx = new AppDbContext(options, currentUser))
        {
            ctx.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = "Test School",
                Code = "T1",
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
            ctx.Users.Add(new UserEntity
            {
                Id = teacherId,
                SchoolId = schoolId,
                Phone = "09000000010",
                Name = "Teacher",
                Role = "Teacher",
            });
            ctx.Homeworks.Add(new HomeworkEntity
            {
                Id = homeworkId,
                SchoolId = schoolId,
                ClassId = classId,
                Subject = "Math",
                Title = "HW",
                Description = "",
                AssignedById = teacherId,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                Status = "Draft",
            });
            ctx.Attachments.Add(new AttachmentEntity
            {
                Id = attachmentId,
                SchoolId = schoolId,
                EntityId = null,
                EntityType = "homework",
                StorageKey = $"{schoolId}/homework/{attachmentId}-{declaredFileName}",
                FileName = declaredFileName,
                ContentType = declaredContentType,
                SizeBytes = declaredSize,
                UploadedById = teacherId,
                UploadedAt = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Pending,
            });
            await ctx.SaveChangesAsync();
        }

        return new Fixture(options, currentUser, attachmentId, homeworkId);
    }

    private sealed record Fixture(
        DbContextOptions<AppDbContext> Options,
        CurrentUserService CurrentUser,
        Guid AttachmentId,
        Guid HomeworkId);
}
