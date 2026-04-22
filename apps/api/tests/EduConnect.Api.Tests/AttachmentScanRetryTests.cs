using System.Net.Sockets;
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
/// Phase 3.2 — the worker retries the scanner on transient I/O failures
/// (SocketException, IOException, TimeoutException) up to three attempts
/// with exponential back-off, then marks the attachment ScanFailed if
/// the scanner still won't talk. Non-transient failures fail closed
/// immediately. Tests pass a zero back-off so they don't sit through
/// the production 2/4-second waits.
/// </summary>
public class AttachmentScanRetryTests
{
    [Fact]
    public async Task Worker_recovers_from_transient_socket_failure_and_marks_Available()
    {
        var (options, attachmentId) = await SeedPendingAttachmentAsync();

        var calls = 0;
        var scanner = new Mock<IAttachmentScanner>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((_, _) =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new SocketException();
                }
                return Task.FromResult(new ScanResult(ScanVerdict.Clean, "mock"));
            });

        await RunWorker(options, scanner.Object);

        await using var verify = new AppDbContext(options);
        var row = await verify.Attachments.SingleAsync(a => a.Id == attachmentId);
        row.Status.Should().Be(AttachmentStatus.Available);
        row.ThreatName.Should().BeNull();
        calls.Should().Be(3);
    }

    [Fact]
    public async Task Worker_marks_ScanFailed_with_scanner_unreachable_after_three_socket_failures()
    {
        var (options, attachmentId) = await SeedPendingAttachmentAsync();

        var scanner = new Mock<IAttachmentScanner>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException());

        await RunWorker(options, scanner.Object);

        await using var verify = new AppDbContext(options);
        var row = await verify.Attachments.SingleAsync(a => a.Id == attachmentId);
        row.Status.Should().Be(AttachmentStatus.ScanFailed);
        row.ThreatName.Should().Be("scanner_unreachable");
        scanner.Verify(
            s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Exactly(AttachmentScanWorker.MaxScanAttempts));
    }

    [Fact]
    public async Task Worker_marks_ScanFailed_with_scan_timeout_after_three_TimeoutException_failures()
    {
        var (options, attachmentId) = await SeedPendingAttachmentAsync();

        var scanner = new Mock<IAttachmentScanner>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("clamd timed out"));

        await RunWorker(options, scanner.Object);

        await using var verify = new AppDbContext(options);
        var row = await verify.Attachments.SingleAsync(a => a.Id == attachmentId);
        row.Status.Should().Be(AttachmentStatus.ScanFailed);
        row.ThreatName.Should().Be("scan_timeout");
    }

    [Fact]
    public async Task Worker_does_not_retry_a_non_transient_scanner_failure()
    {
        var (options, attachmentId) = await SeedPendingAttachmentAsync();

        var scanner = new Mock<IAttachmentScanner>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("malformed reply"));

        await RunWorker(options, scanner.Object);

        await using var verify = new AppDbContext(options);
        var row = await verify.Attachments.SingleAsync(a => a.Id == attachmentId);
        row.Status.Should().Be(AttachmentStatus.ScanFailed);
        row.ThreatName.Should().Be("InvalidOperationException");
        scanner.Verify(
            s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static async Task<(DbContextOptions<AppDbContext> Options, Guid AttachmentId)> SeedPendingAttachmentAsync()
    {
        var schoolId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Retry_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var ctx = new AppDbContext(options);
        ctx.Schools.Add(new SchoolEntity
        {
            Id = schoolId,
            Name = "School",
            Code = "R",
            Address = "",
            ContactPhone = "",
            ContactEmail = "",
        });
        ctx.Attachments.Add(new AttachmentEntity
        {
            Id = attachmentId,
            SchoolId = schoolId,
            EntityType = "homework",
            StorageKey = "school/homework/file.pdf",
            FileName = "file.pdf",
            ContentType = "application/pdf",
            SizeBytes = 9,
            UploadedById = Guid.NewGuid(),
            UploadedAt = DateTimeOffset.UtcNow,
            Status = AttachmentStatus.Pending,
        });
        await ctx.SaveChangesAsync();
        return (options, attachmentId);
    }

    private static async Task RunWorker(
        DbContextOptions<AppDbContext> options,
        IAttachmentScanner scanner)
    {
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n");

        var storage = new Mock<IStorageService>();
        storage
            .Setup(s => s.OpenObjectReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(pdfBytes));

        var worker = new AttachmentScanWorker(
            new RetryScopeFactory(storage.Object, scanner, options),
            new ChannelAttachmentScanQueue(),
            NullLogger<AttachmentScanWorker>.Instance,
            retryBackoff: _ => TimeSpan.Zero);

        var attachmentId = await new AppDbContext(options).Attachments
            .Select(a => a.Id)
            .SingleAsync();

        await worker.ScanOneAsync(attachmentId, CancellationToken.None);
    }

    private sealed class RetryScopeFactory : IServiceScopeFactory
    {
        private readonly IStorageService _storage;
        private readonly IAttachmentScanner _scanner;
        private readonly DbContextOptions<AppDbContext> _options;

        public RetryScopeFactory(
            IStorageService storage,
            IAttachmentScanner scanner,
            DbContextOptions<AppDbContext> options)
        {
            _storage = storage;
            _scanner = scanner;
            _options = options;
        }

        public IServiceScope CreateScope() => new Scope(_storage, _scanner, _options);

        private sealed class Scope : IServiceScope, IServiceProvider
        {
            private readonly IStorageService _storage;
            private readonly IAttachmentScanner _scanner;
            private readonly DbContextOptions<AppDbContext> _options;
            private AppDbContext? _ctx;

            public Scope(IStorageService storage, IAttachmentScanner scanner, DbContextOptions<AppDbContext> options)
            {
                _storage = storage;
                _scanner = scanner;
                _options = options;
            }

            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IStorageService)) return _storage;
                if (serviceType == typeof(IAttachmentScanner)) return _scanner;
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
