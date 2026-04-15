namespace EduConnect.Api.Features.Attachments;

public static class AttachmentFeatureRules
{
    public const int MaxAttachmentsPerEntity = 5;

    public static readonly string[] SupportedEntityTypes =
    [
        "homework",
        "notice"
    ];

    public static readonly string[] HomeworkAllowedContentTypes =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];

    public static readonly string[] HomeworkAllowedExtensions =
    [
        ".pdf",
        ".doc",
        ".docx"
    ];

    public static readonly string[] NoticeAllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    ];

    public static readonly string[] NoticeAllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    ];

    public static IReadOnlyList<string> GetAllowedContentTypes(string entityType) =>
        entityType switch
        {
            "homework" => HomeworkAllowedContentTypes,
            "notice" => NoticeAllowedContentTypes,
            _ => Array.Empty<string>()
        };

    public static IReadOnlyList<string> GetAllowedExtensions(string entityType) =>
        entityType switch
        {
            "homework" => HomeworkAllowedExtensions,
            "notice" => NoticeAllowedExtensions,
            _ => Array.Empty<string>()
        };

    public static bool RequiresForcedDownload(string contentType) =>
        contentType == "application/msword" ||
        contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}
