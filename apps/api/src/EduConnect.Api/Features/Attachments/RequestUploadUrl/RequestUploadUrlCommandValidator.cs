using FluentValidation;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrl;

public class RequestUploadUrlCommandValidator : AbstractValidator<RequestUploadUrlCommand>
{
    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };

    private const int MaxSizeBytes = 10 * 1024 * 1024; // 10MB

    public RequestUploadUrlCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(255).WithMessage("File name must be 255 characters or fewer.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be one of: image/jpeg, image/png, image/webp, application/pdf.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than zero.")
            .LessThanOrEqualTo(MaxSizeBytes).WithMessage("File size must not exceed 10MB.");
    }
}
