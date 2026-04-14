using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Infrastructure.Services;

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(
        IAmazonS3 s3Client,
        IOptions<StorageOptions> storageOptions,
        ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiresIn),
            ContentType = contentType
        };

        var url = _s3Client.GetPreSignedURL(request);

        _logger.LogInformation("Generated presigned upload URL for key {Key}", key);

        return Task.FromResult(url);
    }

    public Task<string> GeneratePresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        string? fileName = null,
        string? contentType = null,
        bool forceDownload = false,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn)
        };

        if (!string.IsNullOrWhiteSpace(fileName) || !string.IsNullOrWhiteSpace(contentType))
        {
            request.ResponseHeaderOverrides = new ResponseHeaderOverrides();

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var dispositionType = forceDownload ? "attachment" : "inline";
                request.ResponseHeaderOverrides.ContentDisposition =
                    $"{dispositionType}; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
            }

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                request.ResponseHeaderOverrides.ContentType = contentType;
            }
        }

        var url = _s3Client.GetPreSignedURL(request);

        return Task.FromResult(url);
    }

    public async Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken);

        _logger.LogInformation("Deleted object with key {Key}", key);
    }
}
