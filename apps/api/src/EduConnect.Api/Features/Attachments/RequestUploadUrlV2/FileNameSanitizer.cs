using System.Globalization;
using System.Text;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrlV2;

/// <summary>
/// Normalises an upload's declared file name into one that's safe to put
/// in a storage key, a Content-Disposition header, and (eventually) a
/// filesystem path on whatever box reads it back.
///
/// Edge cases covered:
///   - <c>Path.GetFileName</c> strips any leading directory components,
///     so <c>../etc/passwd</c> becomes <c>passwd</c>.
///   - Unicode normalisation to NFC so a file like <c>café.pdf</c>
///     written with combining diacritics doesn't round-trip differently
///     when it's later compared or hashed.
///   - Invalid filename characters (.NET's
///     <see cref="Path.GetInvalidFileNameChars"/>) are replaced with
///     <c>-</c>.
///   - Windows reserved device names (CON, PRN, AUX, NUL, COM1..9,
///     LPT1..9) are prefixed with <c>_</c>; Windows treats those as
///     device handles regardless of extension.
///   - Leading dots are prefixed with <c>_</c> so dotfiles such as
///     <c>.htaccess</c> don't disappear when written to a typical
///     filesystem viewer.
///   - Empty / whitespace inputs collapse to <c>file</c>.
/// </summary>
public static class FileNameSanitizer
{
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Sanitize(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return "file";
        }

        // Apply NFC first so the invalid-char strip doesn't have to deal
        // with combining-mark forms; the strip operates on the canonical
        // composed sequence.
        safeFileName = safeFileName.Normalize(NormalizationForm.FormC);

        var builder = new StringBuilder(safeFileName.Length);
        foreach (var character in safeFileName)
        {
            builder.Append(Path.GetInvalidFileNameChars().Contains(character)
                ? '-'
                : character);
        }

        var sanitized = builder.ToString();

        // Re-strip whitespace (rare: a string like " " survives as "-")
        // and bail out if everything got stripped.
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "file";
        }

        if (sanitized.StartsWith('.'))
        {
            sanitized = "_" + sanitized;
        }

        var baseName = Path.GetFileNameWithoutExtension(sanitized);
        if (!string.IsNullOrEmpty(baseName) && WindowsReservedNames.Contains(baseName))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }
}
