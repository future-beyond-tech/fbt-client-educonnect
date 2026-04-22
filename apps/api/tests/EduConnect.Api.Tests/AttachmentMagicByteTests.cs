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
/// Phase 2.4 — the scan worker peeks the first bytes of every uploaded
/// object and compares against <see cref="MimeSignatureValidator"/> before
/// handing the body to ClamAV. A file with a declared MIME that doesn't
/// match its actual bytes (e.g. a PE renamed .pdf) is marked Infected with
/// ThreatName="MIME_MISMATCH" and removed from storage.
/// </summary>
public class AttachmentMagicByteTests
{
    // ── signature table unit tests ──────────────────────────────────────

    public static IEnumerable<object[]> ConsistentSamples => new[]
    {
        new object[] { Bytes("%PDF-1.7\n"), "application/pdf" },
        new object[] { Bytes(0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10), "image/jpeg" },
        new object[] { Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A), "image/png" },
        new object[]
        {
            Concat(Bytes("RIFF"), Bytes(0x00, 0x00, 0x00, 0x00), Bytes("WEBP")),
            "image/webp",
        },
        new object[]
        {
            Bytes(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1),
            "application/msword",
        },
        new object[]
        {
            Bytes(0x50, 0x4B, 0x03, 0x04, 0x14, 0x00),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        },
    };

    [Theory]
    [MemberData(nameof(ConsistentSamples))]
    public void IsConsistent_returns_true_for_matching_prefix(byte[] prefix, string declaredContentType)
    {
        MimeSignatureValidator.IsConsistent(prefix, declaredContentType).Should().BeTrue();
    }

    [Fact]
    public void IsConsistent_returns_false_when_pdf_prefix_is_actually_png()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        MimeSignatureValidator.IsConsistent(pngBytes, "application/pdf").Should().BeFalse();
    }

    [Fact]
    public void IsConsistent_returns_false_for_unknown_declared_content_type()
    {
        var pdfBytes = Bytes("%PDF-");
        MimeSignatureValidator.IsConsistent(pdfBytes, "application/x-msdownload").Should().BeFalse();
    }

    [Fact]
    public void IsConsistent_returns_false_for_empty_prefix()
    {
        MimeSignatureValidator.IsConsistent(ReadOnlySpan<byte>.Empty, "application/pdf").Should().BeFalse();
    }

    // ── worker integration: MIME_MISMATCH path ──────────────────────────

    [Fact]
    public async Task Worker_marks_attachment_Infected_when_magic_bytes_dont_match_declared_content_type()
    {
        var schoolId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MimeMismatch_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var seed = new AppDbContext(options))
        {
            seed.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = "School",
                Code = "X",
                Address = "",
                ContactPhone = "",
                ContactEmail = "",
            });
            seed.Attachments.Add(new AttachmentEntity
            {
                Id = attachmentId,
                SchoolId = schoolId,
                EntityType = "homework",
                StorageKey = "school/homework/fake.pdf",
                FileName = "fake.pdf",
                ContentType = "application/pdf",
                SizeBytes = 8,
                UploadedById = Guid.NewGuid(),
                UploadedAt = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Pending,
            });
            await seed.SaveChangesAsync();
        }

        // Bytes don't start with %PDF-; declared application/pdf will
        // trip the magic-byte pre-check.
        var adversarialBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x00, 0x00, 0x00 };

        var storage = new Mock<IStorageService>();
        storage
            .Setup(s => s.OpenObjectReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(adversarialBytes));
        storage
            .Setup(s => s.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scanner = new Mock<IAttachmentScanner>(MockBehavior.Strict);
        // Scanner must NEVER be called for a MIME mismatch — the
        // pre-check rejects the file first.

        var scopeFactory = new SharedOptionsScopeFactory(storage.Object, scanner.Object, options);

        var worker = new AttachmentScanWorker(
            scopeFactory,
            new ChannelAttachmentScanQueue(),
            NullLogger<AttachmentScanWorker>.Instance);

        await worker.ScanOneAsync(attachmentId, CancellationToken.None);

        await using var verify = new AppDbContext(options);
        var row = await verify.Attachments.SingleAsync(a => a.Id == attachmentId);
        row.Status.Should().Be(AttachmentStatus.Infected);
        row.ThreatName.Should().Be("MIME_MISMATCH");
        row.ScannedAt.Should().NotBeNull();

        storage.Verify(
            s => s.DeleteObjectAsync("school/homework/fake.pdf", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Worker_passes_content_to_scanner_when_magic_bytes_match()
    {
        var schoolId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MimeMatch_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var seed = new AppDbContext(options))
        {
            seed.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = "School",
                Code = "Y",
                Address = "",
                ContactPhone = "",
                ContactEmail = "",
            });
            seed.Attachments.Add(new AttachmentEntity
            {
                Id = attachmentId,
                SchoolId = schoolId,
                EntityType = "homework",
                StorageKey = "school/homework/real.pdf",
                FileName = "real.pdf",
                ContentType = "application/pdf",
                SizeBytes = 9,
                UploadedById = Guid.NewGuid(),
                UploadedAt = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Pending,
            });
            await seed.SaveChangesAsync();
        }

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n");

        var storage = new Mock<IStorageService>();
        storage
            .Setup(s => s.OpenObjectReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(pdfBytes));

        var scanner = new Mock<IAttachmentScanner>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResult(ScanVerdict.Clean, "mock"));

        var scopeFactory = new SharedOptionsScopeFactory(storage.Object, scanner.Object, options);

        var worker = new AttachmentScanWorker(
            scopeFactory,
            new ChannelAttachmentScanQueue(),
            NullLogger<AttachmentScanWorker>.Instance);

        await worker.ScanOneAsync(attachmentId, CancellationToken.None);

        await using var verify = new AppDbContext(options);
        var row = await verify.Attachments.SingleAsync(a => a.Id == attachmentId);
        row.Status.Should().Be(AttachmentStatus.Available);
        row.ThreatName.Should().BeNull();

        scanner.Verify(
            s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static byte[] Bytes(string ascii) => System.Text.Encoding.ASCII.GetBytes(ascii);
    private static byte[] Bytes(params byte[] bytes) => bytes;
    private static byte[] Concat(params byte[][] chunks)
    {
        var total = chunks.Sum(c => c.Length);
        var result = new byte[total];
        var offset = 0;
        foreach (var c in chunks)
        {
            Buffer.BlockCopy(c, 0, result, offset, c.Length);
            offset += c.Length;
        }
        return result;
    }

    /// <summary>
    /// Minimal <see cref="IServiceScopeFactory"/> that hands the worker
    /// the mocks plus a fresh <see cref="AppDbContext"/> pointing at the
    /// seeded in-memory database on every call.
    /// </summary>
    private sealed class SharedOptionsScopeFactory : IServiceScopeFactory
    {
        private readonly IStorageService _storage;
        private readonly IAttachmentScanner _scanner;
        private readonly DbContextOptions<AppDbContext> _options;

        public SharedOptionsScopeFactory(
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
                if (serviceType == typeof(IAttachmentBlockedNotifier)) return NoOpAttachmentBlockedNotifier.Instance;
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
