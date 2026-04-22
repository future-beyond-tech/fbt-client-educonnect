using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using EduConnect.Api.Infrastructure.Services.Scanning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Phase 7.4 — the storage reconciler removes attachment rows whose
/// backing object has gone missing in storage (a delete that succeeded
/// upstream but the DB delete then failed).
/// </summary>
public class AttachmentStorageReconcilerTests
{
    [Fact]
    public async Task Reconciler_removes_rows_whose_storage_object_is_404()
    {
        var schoolId = Guid.NewGuid();
        var dangling = Guid.NewGuid();
        var alive = Guid.NewGuid();
        var options = NewOptions();

        await SeedAsync(options, schoolId,
            BuildAttachment(dangling, schoolId, AttachmentStatus.Available, oldEnough: true),
            BuildAttachment(alive,    schoolId, AttachmentStatus.Available, oldEnough: true));

        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GetObjectMetadataAsync(
                It.Is<string>(k => k.Contains(dangling.ToString())),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageObjectMetadata?)null);
        storage.Setup(s => s.GetObjectMetadataAsync(
                It.Is<string>(k => k.Contains(alive.ToString())),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(2048, "application/pdf"));

        var reconciler = NewReconciler(options, storage.Object);
        var removed = await reconciler.ReconcileOnceAsync(CancellationToken.None);

        removed.Should().Be(1);

        await using var verify = new AppDbContext(options);
        (await verify.Attachments.AnyAsync(a => a.Id == dangling)).Should().BeFalse();
        (await verify.Attachments.AnyAsync(a => a.Id == alive)).Should().BeTrue();
    }

    [Fact]
    public async Task Reconciler_does_not_consider_rows_within_grace_window()
    {
        var schoolId = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        var options = NewOptions();

        await SeedAsync(options, schoolId,
            BuildAttachment(fresh, schoolId, AttachmentStatus.Available, oldEnough: false));

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        // Strict mock: if reconciler asks about the fresh row it'll throw.

        var reconciler = NewReconciler(options, storage.Object);
        var removed = await reconciler.ReconcileOnceAsync(CancellationToken.None);

        removed.Should().Be(0);
        storage.Verify(s => s.GetObjectMetadataAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await using var verify = new AppDbContext(options);
        (await verify.Attachments.AnyAsync(a => a.Id == fresh)).Should().BeTrue();
    }

    [Fact]
    public async Task Reconciler_skips_Infected_rows()
    {
        var schoolId = Guid.NewGuid();
        var infected = Guid.NewGuid();
        var options = NewOptions();

        await SeedAsync(options, schoolId,
            BuildAttachment(infected, schoolId, AttachmentStatus.Infected, oldEnough: true));

        var storage = new Mock<IStorageService>(MockBehavior.Strict);

        var reconciler = NewReconciler(options, storage.Object);
        var removed = await reconciler.ReconcileOnceAsync(CancellationToken.None);

        removed.Should().Be(0);
        storage.Verify(s => s.GetObjectMetadataAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> NewOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"StorageReconciler_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static AttachmentStorageReconciler NewReconciler(
        DbContextOptions<AppDbContext> options,
        IStorageService storage)
    {
        return new AttachmentStorageReconciler(
            new SingleScopeFactory(options, storage),
            NullLogger<AttachmentStorageReconciler>.Instance,
            interval: TimeSpan.FromMilliseconds(50),
            minAge: TimeSpan.FromMinutes(5));
    }

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        params AttachmentEntity[] attachments)
    {
        await using var ctx = new AppDbContext(options);
        if (!await ctx.Schools.AnyAsync(s => s.Id == schoolId))
        {
            ctx.Schools.Add(new SchoolEntity
            {
                Id = schoolId, Name = "S", Code = $"S-{schoolId.ToString()[..6]}",
                Address = "", ContactPhone = "", ContactEmail = "",
            });
        }
        ctx.Attachments.AddRange(attachments);
        await ctx.SaveChangesAsync();
    }

    private static AttachmentEntity BuildAttachment(
        Guid id, Guid schoolId, string status, bool oldEnough) =>
        new()
        {
            Id = id, SchoolId = schoolId,
            EntityType = "homework", EntityId = Guid.NewGuid(),
            FileName = "f.pdf", ContentType = "application/pdf",
            SizeBytes = 1024,
            StorageKey = $"{schoolId}/homework/{id}-f.pdf",
            UploadedById = Guid.NewGuid(),
            UploadedAt = oldEnough
                ? DateTimeOffset.UtcNow.AddHours(-2)
                : DateTimeOffset.UtcNow,
            Status = status,
        };

    /// <summary>
    /// Hands the reconciler a fresh AppDbContext + the supplied
    /// IStorageService per scope.
    /// </summary>
    private sealed class SingleScopeFactory : IServiceScopeFactory
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly IStorageService _storage;
        public SingleScopeFactory(DbContextOptions<AppDbContext> options, IStorageService storage)
        {
            _options = options;
            _storage = storage;
        }
        public IServiceScope CreateScope() => new Scope(_options, _storage);

        private sealed class Scope : IServiceScope, IServiceProvider
        {
            private readonly DbContextOptions<AppDbContext> _options;
            private readonly IStorageService _storage;
            private AppDbContext? _ctx;

            public Scope(DbContextOptions<AppDbContext> options, IStorageService storage)
            {
                _options = options;
                _storage = storage;
            }
            public IServiceProvider ServiceProvider => this;
            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IStorageService)) return _storage;
                if (serviceType == typeof(AppDbContext))
                {
                    _ctx ??= new AppDbContext(_options);
                    return _ctx;
                }
                return null;
            }
            public void Dispose() => _ctx?.Dispose();
        }
    }
}
