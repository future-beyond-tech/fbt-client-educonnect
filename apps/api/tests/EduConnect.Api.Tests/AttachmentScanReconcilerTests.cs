using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services.Scanning;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Phase 3.1 — the reconciler runs at host startup and re-enqueues any
/// attachment that's still <c>Pending</c> from before a restart, so the
/// volatile in-memory queue doesn't silently lose work.
/// </summary>
public class AttachmentScanReconcilerTests
{
    [Fact]
    public async Task Reconciler_re_enqueues_pending_attachments_older_than_grace_window()
    {
        var schoolId = Guid.NewGuid();
        var staleId = Guid.NewGuid();

        var options = NewOptions();
        await SeedAsync(options, schoolId, attachments:
        [
            BuildAttachment(staleId, schoolId, AttachmentStatus.Pending, uploadedAt: DateTimeOffset.UtcNow.AddMinutes(-10)),
        ]);

        var queue = new ChannelAttachmentScanQueue();
        var reconciler = NewReconciler(options, queue, graceMinutes: 2);

        var count = await reconciler.ReconcileAsync(CancellationToken.None);

        count.Should().Be(1);
        var dequeued = await DequeueOneAsync(queue);
        dequeued.Should().Be(staleId);
    }

    [Fact]
    public async Task Reconciler_skips_pending_attachments_within_grace_window()
    {
        var schoolId = Guid.NewGuid();
        var freshId = Guid.NewGuid();

        var options = NewOptions();
        await SeedAsync(options, schoolId, attachments:
        [
            BuildAttachment(freshId, schoolId, AttachmentStatus.Pending, uploadedAt: DateTimeOffset.UtcNow),
        ]);

        var queue = new ChannelAttachmentScanQueue();
        var reconciler = NewReconciler(options, queue, graceMinutes: 2);

        var count = await reconciler.ReconcileAsync(CancellationToken.None);

        count.Should().Be(0);
        (await IsQueueEmptyAsync(queue)).Should().BeTrue();
    }

    [Fact]
    public async Task Reconciler_skips_attachments_already_in_terminal_states()
    {
        var schoolId = Guid.NewGuid();
        var available = Guid.NewGuid();
        var infected = Guid.NewGuid();
        var failed = Guid.NewGuid();

        var options = NewOptions();
        var oldUploadedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await SeedAsync(options, schoolId, attachments:
        [
            BuildAttachment(available, schoolId, AttachmentStatus.Available, uploadedAt: oldUploadedAt),
            BuildAttachment(infected,  schoolId, AttachmentStatus.Infected,  uploadedAt: oldUploadedAt),
            BuildAttachment(failed,    schoolId, AttachmentStatus.ScanFailed, uploadedAt: oldUploadedAt),
        ]);

        var queue = new ChannelAttachmentScanQueue();
        var reconciler = NewReconciler(options, queue, graceMinutes: 2);

        var count = await reconciler.ReconcileAsync(CancellationToken.None);

        count.Should().Be(0);
        (await IsQueueEmptyAsync(queue)).Should().BeTrue();
    }

    [Fact]
    public async Task Reconciler_works_across_multiple_tenants_via_RLS_bypass()
    {
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        var rowA = Guid.NewGuid();
        var rowB = Guid.NewGuid();

        var options = NewOptions();
        var oldUploadedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await SeedAsync(options, schoolA, attachments:
        [
            BuildAttachment(rowA, schoolA, AttachmentStatus.Pending, uploadedAt: oldUploadedAt),
        ]);
        await SeedAsync(options, schoolB, attachments:
        [
            BuildAttachment(rowB, schoolB, AttachmentStatus.Pending, uploadedAt: oldUploadedAt),
        ]);

        var queue = new ChannelAttachmentScanQueue();
        var reconciler = NewReconciler(options, queue, graceMinutes: 2);

        var count = await reconciler.ReconcileAsync(CancellationToken.None);

        count.Should().Be(2);
        var first = await DequeueOneAsync(queue);
        var second = await DequeueOneAsync(queue);
        new[] { first, second }.Should().BeEquivalentTo(new[] { rowA, rowB });
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> NewOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Reconciler_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static AttachmentScanReconciler NewReconciler(
        DbContextOptions<AppDbContext> options,
        IAttachmentScanQueue queue,
        int graceMinutes)
    {
        return new AttachmentScanReconciler(
            new SingleScopeFactory(options),
            queue,
            Options.Create(new AttachmentScannerOptions { ReconciliationGraceMinutes = graceMinutes }),
            NullLogger<AttachmentScanReconciler>.Instance);
    }

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<AttachmentEntity>? attachments = null)
    {
        await using var ctx = new AppDbContext(options);
        if (!await ctx.Schools.AnyAsync(s => s.Id == schoolId))
        {
            ctx.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = "S",
                Code = $"S-{schoolId.ToString()[..6]}",
                Address = "",
                ContactPhone = "",
                ContactEmail = "",
            });
        }
        if (attachments is not null)
        {
            ctx.Attachments.AddRange(attachments);
        }
        await ctx.SaveChangesAsync();
    }

    private static AttachmentEntity BuildAttachment(
        Guid id,
        Guid schoolId,
        string status,
        DateTimeOffset uploadedAt) =>
        new()
        {
            Id = id,
            SchoolId = schoolId,
            EntityType = "homework",
            StorageKey = $"{schoolId}/{id}",
            FileName = "f.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1,
            UploadedById = Guid.NewGuid(),
            UploadedAt = uploadedAt,
            Status = status,
        };

    private static async Task<Guid> DequeueOneAsync(IAttachmentScanQueue queue)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var id in queue.DequeueAllAsync(cts.Token))
        {
            return id;
        }
        throw new InvalidOperationException("queue had no items");
    }

    private static async Task<bool> IsQueueEmptyAsync(IAttachmentScanQueue queue)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        try
        {
            await foreach (var _ in queue.DequeueAllAsync(cts.Token))
            {
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            return true;
        }
        return true;
    }

    /// <summary>
    /// Hands out a fresh AppDbContext per scope so the reconciler's
    /// CreateAsyncScope contract is honoured against the same in-memory
    /// backing store.
    /// </summary>
    private sealed class SingleScopeFactory : IServiceScopeFactory
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public SingleScopeFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public IServiceScope CreateScope() => new Scope(_options);

        private sealed class Scope : IServiceScope, IServiceProvider
        {
            private readonly DbContextOptions<AppDbContext> _options;
            private AppDbContext? _ctx;
            public Scope(DbContextOptions<AppDbContext> options) => _options = options;
            public IServiceProvider ServiceProvider => this;
            public object? GetService(Type serviceType)
            {
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
