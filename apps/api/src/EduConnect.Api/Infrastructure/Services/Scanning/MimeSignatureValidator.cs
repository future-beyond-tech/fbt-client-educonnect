namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Hand-rolled magic-byte table for the MIME types the attachment subsystem
/// accepts (see <c>AttachmentFeatureRules</c>). Dependency-free because the
/// allowed set is small and stable; no general-purpose MIME detector is
/// worth the supply-chain footprint for six signatures.
///
/// Returns <c>false</c> for any declared content type we don't recognise,
/// which is intentional — an attachment row should never reach the scanner
/// with a content type the upload validator didn't pre-approve.
/// </summary>
public static class MimeSignatureValidator
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="prefix"/> begins with a byte
    /// sequence consistent with <paramref name="declaredContentType"/>.
    /// </summary>
    public static bool IsConsistent(ReadOnlySpan<byte> prefix, string declaredContentType)
    {
        return declaredContentType switch
        {
            "application/pdf"  => StartsWith(prefix, "%PDF-"u8),
            "image/jpeg"       => StartsWith(prefix, JpegSignature),
            "image/png"        => StartsWith(prefix, PngSignature),
            "image/webp"       => IsWebp(prefix),
            "application/msword" => StartsWith(prefix, DocOle2Signature),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                               => IsZipContainer(prefix),
            _ => false,
        };
    }

    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };

    private static readonly byte[] PngSignature =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly byte[] DocOle2Signature =
        { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    private static bool IsWebp(ReadOnlySpan<byte> prefix)
    {
        // RIFF container: bytes 0..3 = "RIFF", bytes 8..11 = "WEBP". Bytes
        // 4..7 hold the file-size minus 8 and aren't checked here.
        if (prefix.Length < 12) return false;
        return StartsWith(prefix, "RIFF"u8) && StartsWith(prefix[8..], "WEBP"u8);
    }

    private static bool IsZipContainer(ReadOnlySpan<byte> prefix)
    {
        // ZIP local-file / central-directory / spanning-signature headers.
        // DOCX is always a ZIP; a non-DOCX ZIP would also match here, but
        // the V2 validator only admits DOCX content types, so the extra
        // differentiation is wasted work for this phase.
        if (prefix.Length < 4) return false;
        return prefix[0] == 0x50
            && prefix[1] == 0x4B
            && (prefix[2] == 0x03 || prefix[2] == 0x05 || prefix[2] == 0x07);
    }

    private static bool StartsWith(ReadOnlySpan<byte> input, ReadOnlySpan<byte> target)
    {
        return input.Length >= target.Length
            && input[..target.Length].SequenceEqual(target);
    }
}
