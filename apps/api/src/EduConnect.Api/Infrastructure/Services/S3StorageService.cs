using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using EduConnect.Api.Common.Exceptions;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Infrastructure.Services;

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(
        IAmazonS3 s3Client,
        IHttpClientFactory httpClientFactory,
        IOptions<StorageOptions> storageOptions,
        ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _httpClientFactory = httpClientFactory;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task EnsureUploadTargetAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(
                _s3Client,
                _storageOptions.BucketName);

            if (!bucketExists)
            {
                throw new StorageException(
                    $"Storage bucket '{_storageOptions.BucketName}' is unavailable or the configured credentials cannot access it.");
            }
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException(
                $"Failed to reach storage bucket '{_storageOptions.BucketName}'.",
                ex);
        }
    }

    public async Task UploadObjectAsync(
        string key,
        Stream content,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var uploadUrl = await GeneratePresignedUploadUrlAsync(
            key,
            contentType,
            sizeBytes,
            TimeSpan.FromMinutes(_storageOptions.PresignedUploadExpiryMinutes),
            cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        streamContent.Headers.ContentLength = sizeBytes;
        request.Content = streamContent;

        using var httpClient = _httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = string.IsNullOrWhiteSpace(body)
                ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
                : $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}";

            throw new StorageException(
                $"Failed to upload object with key {key}. Storage returned {detail}");
        }

        _logger.LogInformation(
            "Uploaded object with key {Key} via server-side fallback ({SizeBytes} bytes)",
            key,
            sizeBytes);
    }

    public Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        long sizeBytes,
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

        // Pin the signed request to the exact byte count so S3 rejects any
        // PUT whose Content-Length header differs. The AWS SDK v3 PUT-URL
        // path does not expose content-length-range (that's POST-policy);
        // including Content-Length in the signed headers delegates
        // enforcement to the storage backend.
        request.Headers["Content-Length"] = sizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            var url = _s3Client.GetPreSignedURL(request);

            _logger.LogInformation(
                "Generated presigned upload URL for key {Key} pinned to {SizeBytes} bytes",
                key,
                sizeBytes);

            return Task.FromResult(url);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Failed to mint presigned upload URL for key {key}.", ex);
        }
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

        try
        {
            var url = _s3Client.GetPreSignedURL(request);
            return Task.FromResult(url);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Failed to mint presigned download URL for key {key}.", ex);
        }
    }

    public async Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = key
        };

        try
        {
            await _s3Client.DeleteObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Failed to delete object with key {key}.", ex);
        }

        _logger.LogInformation("Deleted object with key {Key}", key);
    }

    public async Task<Stream> OpenObjectReadStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _storageOptions.BucketName,
                Key = key,
            }, cancellationToken);

            // Caller disposes — wrapping ensures the S3 response is released
            // when the stream is closed.
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Failed to open object stream for key {key}.", ex);
        }
    }

    public async Task<StorageObjectMetadata?> GetObjectMetadataAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _storageOptions.BucketName,
                Key = key,
            }, cancellationToken);

            return new StorageObjectMetadata(
                response.ContentLength,
                response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Failed to load object metadata for key {key}.", ex);
        }
    }
}
