namespace EduConnect.Api.Features.Attachments;

public static class AttachmentFeatureRules
{
    public const int MaxAttachmentsPerEntity = 5;

    public static readonly string[] SupportedEntityTypes =
    [
        "homework",
        "homework_submission",
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

    // Student submissions allow the same set as the homework itself plus
    // common image formats (photo of handwritten work).
    public static readonly string[] HomeworkSubmissionAllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];

    public static readonly string[] HomeworkSubmissionAllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
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
            "homework_submission" => HomeworkSubmissionAllowedContentTypes,
            "notice" => NoticeAllowedContentTypes,
            _ => Array.Empty<string>()
        };

    public static IReadOnlyList<string> GetAllowedExtensions(string entityType) =>
        entityType switch
        {
            "homework" => HomeworkAllowedExtensions,
            "homework_submission" => HomeworkSubmissionAllowedExtensions,
            "notice" => NoticeAllowedExtensions,
            _ => Array.Empty<string>()
        };

    public static bool RequiresForcedDownload(string contentType) =>
        contentType == "application/msword" ||
        contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}
