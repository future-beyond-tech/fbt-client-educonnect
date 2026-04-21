namespace EduConnect.Api.Infrastructure.Services;

public interface IStorageService
{
    /// <summary>
    /// Generates a presigned URL for uploading a file directly to storage.
    /// </summary>
    Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned URL for downloading a file directly from storage.
    /// </summary>
    Task<string> GeneratePresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        string? fileName = null,
        string? contentType = null,
        bool forceDownload = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from storage.
    /// </summary>
    Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream directly from storage. Caller owns the stream and
    /// must dispose it. Used by the virus-scan pipeline to hand the object
    /// body to the scanner without a staging round-trip through disk.
    /// </summary>
    Task<Stream> OpenObjectReadStreamAsync(string key, CancellationToken cancellationToken = default);
}
