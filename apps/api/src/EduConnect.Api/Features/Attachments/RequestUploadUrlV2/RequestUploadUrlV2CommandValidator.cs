using EduConnect.Api.Features.Attachments;
using EduConnect.Api.Infrastructure.Services;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrlV2;

public class RequestUploadUrlV2CommandValidator : AbstractValidator<RequestUploadUrlV2Command>
{
    public RequestUploadUrlV2CommandValidator(IOptions<StorageOptions> storageOptions)
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(255).WithMessage("File name must be 255 characters or fewer.");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(entityType => AttachmentFeatureRules.SupportedEntityTypes.Contains(entityType))
            .WithMessage("Entity type must be 'homework' or 'notice'.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must((command, contentType) =>
                AttachmentFeatureRules.GetAllowedContentTypes(command.EntityType).Contains(contentType))
            .WithMessage("This file type is not allowed for the selected entity.");

        RuleFor(x => x.FileName)
            .Must((command, fileName) =>
            {
                var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
                return AttachmentFeatureRules.GetAllowedExtensions(command.EntityType).Contains(extension);
            })
            .WithMessage("Invalid file extension for the selected entity.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than zero.")
            .LessThanOrEqualTo((int)storageOptions.Value.MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {storageOptions.Value.MaxFileSizeBytes / (1024 * 1024)}MB.");
    }
}
