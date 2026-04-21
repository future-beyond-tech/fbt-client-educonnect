using EduConnect.Api.Common.Auth;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;
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

public class AttachmentScanFlowTests
{
    [Fact]
    public async Task GetAttachmentsForEntity_filters_out_non_available_rows()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var currentUser = new CurrentUserService
        {
            SchoolId = schoolId,
            UserId = teacherId,
            Role = "Teacher",
            Name = "Test Teacher",
        };

        await using (var context = new AppDbContext(options, currentUser))
        {
            context.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = "Test School",
                Code = "TEST",
                Address = "",
                ContactPhone = "",
                ContactEmail = "",
            });
            context.Classes.Add(new ClassEntity
            {
                Id = classId,
                SchoolId = schoolId,
                Name = "6",
                Section = "A",
                AcademicYear = "2026",
            });
            context.Users.Add(new UserEntity
            {
                Id = teacherId,
                SchoolId = schoolId,
                Name = "Teacher",
                Phone = "09000000001",
                Role = "Teacher",
            });
            context.Homeworks.Add(new HomeworkEntity
            {
                Id = homeworkId,
                SchoolId = schoolId,
                ClassId = classId,
                Subject = "Math",
                Title = "HW 1",
                Description = "",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                Status = "Published",
                AssignedById = teacherId,
            });

            foreach (var status in new[]
            {
                AttachmentStatus.Pending,
                AttachmentStatus.Available,
                AttachmentStatus.Infected,
                AttachmentStatus.ScanFailed,
            })
            {
                context.Attachments.Add(new AttachmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    EntityId = homeworkId,
                    EntityType = "homework",
                    StorageKey = $"test/{status}.pdf",
                    FileName = $"{status}.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 100,
                    UploadedById = teacherId,
                    UploadedAt = DateTimeOffset.UtcNow,
                    Status = status,
                });
            }

            await context.SaveChangesAsync();
        }

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        storage.Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed/");

        await using var readContext = new AppDbContext(options, currentUser);
        var handler = new GetAttachmentsForEntityQueryHandler(readContext, currentUser, storage.Object);

        var result = await handler.Handle(
            new GetAttachmentsForEntityQuery(homeworkId, "homework"),
            CancellationToken.None);

        result.Should().ContainSingle(
            "only rows with Status=Available should be handed out for download");
        result[0].FileName.Should().Be($"{AttachmentStatus.Available}.pdf");

        // Presigned URL must have been requested exactly once — the Pending,
        // Infected, and ScanFailed rows never reach the storage service.
        storage.Verify(
            s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoOpAttachmentScanner_returns_clean_and_drains_stream()
    {
        var scanner = new NoOpAttachmentScanner(NullLogger<NoOpAttachmentScanner>.Instance);
        var payload = new byte[4096];
        Random.Shared.NextBytes(payload);
        await using var stream = new MemoryStream(payload);

        var result = await scanner.ScanAsync(stream);

        result.IsClean.Should().BeTrue();
        result.Engine.Should().Be(NoOpAttachmentScanner.EngineName);
        result.ThreatName.Should().BeNull();
        stream.Position.Should().Be(payload.Length, "the scanner should drain the entire stream");
    }

    [Fact]
    public async Task ChannelAttachmentScanQueue_round_trips_ids_in_order()
    {
        var queue = new ChannelAttachmentScanQueue();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await queue.EnqueueAsync(a);
        await queue.EnqueueAsync(b);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<Guid>();
        await foreach (var id in queue.DequeueAllAsync(cts.Token))
        {
            received.Add(id);
            if (received.Count == 2) break;
        }

        received.Should().Equal(a, b);
    }
}
