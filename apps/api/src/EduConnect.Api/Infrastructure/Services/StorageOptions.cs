namespace EduConnect.Api.Infrastructure.Services;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string BucketName { get; set; } = "educonnect-attachments";
    public string Region { get; set; } = "ap-south-1";
    public string? ServiceUrl { get; set; }
    public int PresignedUploadExpiryMinutes { get; set; } = 15;
    public int PresignedDownloadExpiryMinutes { get; set; } = 60;
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
