namespace EduConnect.Api.Infrastructure.Services;

public interface IStorageService
{
    /// <summary>
    /// Generates a presigned URL for uploading a file directly to storage.
    /// The signed request pins <c>Content-Length</c> to <paramref name="sizeBytes"/>
    /// so the storage backend rejects uploads whose actual body size
    /// differs from the caller's declared size. The client must send the
    /// PUT with a matching <c>Content-Length</c> header (browsers do this
    /// automatically for a Blob/File body).
    /// </summary>
    Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        long sizeBytes,
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

    /// <summary>
    /// Returns size + content-type for the stored object, or <c>null</c>
    /// if no object exists at the supplied key. Used by the attach path
    /// to verify the client actually PUT a file matching the declared
    /// size/type before linking the attachment row to an entity — so a
    /// misbehaving client cannot cause the DB row to point at a phantom
    /// or size/type-forged object.
    /// </summary>
    Task<StorageObjectMetadata?> GetObjectMetadataAsync(
        string key,
        CancellationToken cancellationToken = default);
}

public sealed record StorageObjectMetadata(long SizeBytes, string? ContentType);
