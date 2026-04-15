using EduConnect.Api.Features.Attachments;
using EduConnect.Api.Infrastructure.Services;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrl;

public class RequestUploadUrlCommandValidator : AbstractValidator<RequestUploadUrlCommand>
{
    public RequestUploadUrlCommandValidator(IOptions<StorageOptions> storageOptions)
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(255).WithMessage("File name must be 255 characters or fewer.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(ct => AttachmentFeatureRules.NoticeAllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be one of: image/jpeg, image/png, image/webp, application/pdf.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than zero.")
            .LessThanOrEqualTo((int)storageOptions.Value.MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {storageOptions.Value.MaxFileSizeBytes / (1024 * 1024)}MB.");
    }
}
