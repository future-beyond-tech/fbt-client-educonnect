using EduConnect.Api.Common.Auth;
using EduConnect.Api.Features.Notices.GetNotices;

namespace EduConnect.Api.Features.Notices;

// Mirrors the authorization rules enforced by UpdateNoticeCommandHandler,
// PublishNoticeCommandHandler, and AttachFileToEntity/DeleteAttachment for
// `notice` entities. UI reads these to decide which actions to expose on
// draft cards and the preview page without trial-and-error requests.
public static class NoticeCapabilities
{
    public static NoticeCapabilitiesDto For(
        CurrentUserService currentUser,
        bool isPublished,
        Guid publishedById)
    {
        var isAdmin = currentUser.Role == "Admin";
        var isDraft = !isPublished;
        var isCreator = currentUser.UserId == publishedById;

        return new NoticeCapabilitiesDto(
            CanEditDraft: isAdmin && isDraft && isCreator,
            CanManageDraftAttachments: isAdmin && isDraft,
            CanPreviewDraft: isAdmin && isDraft,
            CanPublishDraft: isAdmin && isDraft);
    }
}
