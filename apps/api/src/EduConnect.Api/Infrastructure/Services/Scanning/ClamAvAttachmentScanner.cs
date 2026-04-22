using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Speaks clamd's INSTREAM protocol directly over TCP. Deliberately avoids
/// a third-party client dependency — the wire format is short enough that
/// inlining it keeps the attack surface minimal and upgrades cheap.
///
/// Wire protocol (from clamd(8)):
///   1. Connect to Host:Port.
///   2. Send command "zINSTREAM\0".
///   3. Send N chunks: 4 bytes BE length + chunk payload.
///   4. Send terminating 4 bytes of zero length.
///   5. Read reply until NUL. Expected replies:
///        "stream: OK"                -> Clean
///        "stream: &lt;Threat&gt; FOUND"   -> Infected (parse threat name)
///        other                        -> Error
/// </summary>
public sealed class ClamAvAttachmentScanner : IAttachmentScanner
{
    public const string EngineName = "clamav";
    private const int ChunkSize = 64 * 1024;

    private readonly AttachmentScannerOptions _options;
    private readonly ILogger<ClamAvAttachmentScanner> _logger;

    public ClamAvAttachmentScanner(
        IOptions<AttachmentScannerOptions> options,
        ILogger<ClamAvAttachmentScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var ct = timeoutCts.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, ct);
            await using var netStream = client.GetStream();

            await netStream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), ct);

            var chunkBuffer = new byte[ChunkSize];
            var lengthBuffer = new byte[4];

            while (true)
            {
                var read = await content.ReadAsync(chunkBuffer.AsMemory(0, ChunkSize), ct);
                if (read <= 0) break;

                BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, (uint)read);
                await netStream.WriteAsync(lengthBuffer, ct);
                await netStream.WriteAsync(chunkBuffer.AsMemory(0, read), ct);
            }

            // Zero-length terminator.
            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, 0u);
            await netStream.WriteAsync(lengthBuffer, ct);
            await netStream.FlushAsync(ct);

            var replyBuilder = new StringBuilder();
            var readBuffer = new byte[256];
            int n;
            while ((n = await netStream.ReadAsync(readBuffer, ct)) > 0)
            {
                replyBuilder.Append(Encoding.ASCII.GetString(readBuffer, 0, n));
                if (replyBuilder.Length > 0 && replyBuilder[^1] == '\0')
                {
                    break;
                }
            }

            return ParseReply(replyBuilder.ToString().Trim('\0', ' ', '\n', '\r'));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancellation — propagate so the worker can shut down
            // cleanly without the scanner masking the cancellation as an
            // Error verdict.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Internal timeout (linked CTS fired). Surface as a typed
            // TimeoutException so the worker's retry loop can recognise
            // it as transient.
            _logger.LogWarning(
                "ClamAV scan timed out after {Timeout}s against {Host}:{Port}",
                _options.TimeoutSeconds, _options.Host, _options.Port);
            throw new TimeoutException(
                $"ClamAV scan timed out after {_options.TimeoutSeconds}s against {_options.Host}:{_options.Port}",
                ex);
        }
        // SocketException, IOException and any other I/O failure
        // propagate to the caller. The worker's retry loop classifies
        // them as transient (worth a retry) or terminal (mark
        // ScanFailed). Catching them here would deny the worker the
        // chance to retry across a transient blip.
    }

    private ScanResult ParseReply(string reply)
    {
        // Clean: "stream: OK"
        if (reply.EndsWith("OK", StringComparison.Ordinal))
        {
            return new ScanResult(ScanVerdict.Clean, EngineName);
        }

        // Infected: "stream: <ThreatName> FOUND"
        if (reply.EndsWith("FOUND", StringComparison.Ordinal))
        {
            var colonIdx = reply.IndexOf(':');
            var threat = colonIdx >= 0
                ? reply[(colonIdx + 1)..^"FOUND".Length].Trim()
                : "unknown";
            _logger.LogWarning("ClamAV flagged stream as infected: {Threat}", threat);
            return new ScanResult(ScanVerdict.Infected, EngineName, threat);
        }

        _logger.LogError("ClamAV returned an unexpected reply: {Reply}", reply);
        return new ScanResult(ScanVerdict.Error, EngineName, "unexpected_reply");
    }
}
