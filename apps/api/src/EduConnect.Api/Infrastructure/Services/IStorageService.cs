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
}
