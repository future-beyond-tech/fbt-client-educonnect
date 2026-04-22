using EduConnect.Api.Common.Auth;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using EduConnect.Api.Infrastructure.Services.Scanning;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class AttachmentScanFlowTests
{
    [Fact]
    public async Task GetAttachmentsForEntity_returns_in_progress_statuses_to_teachers_with_download_url_only_for_Available()
    {
        var (options, _, homeworkId, teacherId) = await SeedHomeworkWithAllStatusesAsync();
        var teacher = new CurrentUserService
        {
            SchoolId = (await new AppDbContext(options).Schools.Select(s => s.Id).FirstAsync()),
            UserId = teacherId,
            Role = "Teacher",
            Name = "Teacher",
        };

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        storage.Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed/");

        await using var readContext = new AppDbContext(options, teacher);
        var handler = new GetAttachmentsForEntityQueryHandler(
            readContext, teacher, storage.Object,
            Options.Create(new StorageOptions()),
            new HttpContextAccessor());

        var result = await handler.Handle(
            new GetAttachmentsForEntityQuery(homeworkId, "homework"),
            CancellationToken.None);

        // Teacher sees Pending + Available + ScanFailed; Infected stays
        // hidden (admins are notified out-of-band).
        result.Select(r => r.Status).Should().BeEquivalentTo(new[]
        {
            AttachmentStatus.Pending,
            AttachmentStatus.Available,
            AttachmentStatus.ScanFailed,
        });

        // Presigned URL minted only for the Available row — the read path
        // never hands out an unscanned object even when the badge is shown.
        result.Single(r => r.Status == AttachmentStatus.Available).DownloadUrl
            .Should().Be("https://signed/");
        result.Single(r => r.Status == AttachmentStatus.Pending).DownloadUrl.Should().BeNull();
        result.Single(r => r.Status == AttachmentStatus.ScanFailed).DownloadUrl.Should().BeNull();

        storage.Verify(
            s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAttachmentsForEntity_returns_only_Available_to_parents()
    {
        var (options, schoolId, homeworkId, teacherId) = await SeedHomeworkWithAllStatusesAsync();

        var parentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classId = await new AppDbContext(options).Homeworks
            .Where(h => h.Id == homeworkId).Select(h => h.ClassId).FirstAsync();

        await using (var ctx = new AppDbContext(options))
        {
            ctx.Users.Add(new UserEntity
            {
                Id = parentId, SchoolId = schoolId, Phone = "09000000099",
                Name = "Parent", Role = "Parent",
            });
            ctx.Students.Add(new StudentEntity
            {
                Id = studentId, SchoolId = schoolId, ClassId = classId,
                Name = "Child", RollNumber = "001", IsActive = true,
            });
            ctx.ParentStudentLinks.Add(new ParentStudentLinkEntity
            {
                Id = Guid.NewGuid(), SchoolId = schoolId,
                ParentId = parentId, StudentId = studentId, Relationship = "parent",
            });
            await ctx.SaveChangesAsync();
        }

        var parent = new CurrentUserService
        {
            SchoolId = schoolId, UserId = parentId, Role = "Parent", Name = "Parent",
        };

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        storage.Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signed/");

        await using var readContext = new AppDbContext(options, parent);
        var handler = new GetAttachmentsForEntityQueryHandler(
            readContext, parent, storage.Object,
            Options.Create(new StorageOptions()),
            new HttpContextAccessor());

        var result = await handler.Handle(
            new GetAttachmentsForEntityQuery(homeworkId, "homework"),
            CancellationToken.None);

        result.Should().ContainSingle(
            "parents should never see Pending / Infected / ScanFailed rows");
        result[0].Status.Should().Be(AttachmentStatus.Available);
        result[0].DownloadUrl.Should().Be("https://signed/");
    }

    private static async Task<(DbContextOptions<AppDbContext> Options, Guid SchoolId, Guid HomeworkId, Guid TeacherId)>
        SeedHomeworkWithAllStatusesAsync()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new AppDbContext(options);
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

        var nowSeed = DateTimeOffset.UtcNow;
        var i = 0;
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
                UploadedAt = nowSeed.AddSeconds(i++),
                Status = status,
            });
        }

        await context.SaveChangesAsync();
        return (options, schoolId, homeworkId, teacherId);
    }

    [Fact]
    public async Task NoOpAttachmentScanner_fails_closed_with_Error_verdict_and_drains_stream()
    {
        var scanner = new NoOpAttachmentScanner(NullLogger<NoOpAttachmentScanner>.Instance);
        var payload = new byte[4096];
        Random.Shared.NextBytes(payload);
        await using var stream = new MemoryStream(payload);

        var result = await scanner.ScanAsync(stream);

        result.IsClean.Should().BeFalse("the dev/CI stub must not silently approve uploads");
        result.IsError.Should().BeTrue();
        result.Verdict.Should().Be(ScanVerdict.Error);
        result.Engine.Should().Be(NoOpAttachmentScanner.EngineName);
        result.ThreatName.Should().Be(NoOpAttachmentScanner.NoOpThreatName);
        stream.Position.Should().Be(payload.Length, "the scanner should still drain the entire stream");
    }

    [Fact]
    public void AttachmentScannerRegistration_throws_when_scanner_disabled_in_Production()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new AttachmentScannerOptions { Enabled = false };
        var environment = new FakeHostEnvironment(Environments.Production);

        var act = () => services.AddAttachmentScanner(options, environment);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*CLAMAV_ENABLED must be true in Production*");
    }

    [Fact]
    public void AttachmentScannerRegistration_registers_NoOp_in_Development()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new AttachmentScannerOptions { Enabled = false };
        var environment = new FakeHostEnvironment(Environments.Development);

        services.AddAttachmentScanner(options, environment);

        using var provider = services.BuildServiceProvider();
        var scanner = provider.GetRequiredService<IAttachmentScanner>();
        scanner.Should().BeOfType<NoOpAttachmentScanner>();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "EduConnect.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
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
