"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet, apiPut } from "@/lib/api-client";
import {
  buildAttachmentDownloadUrl,
  normalizeAttachmentViewUrl,
} from "@/lib/attachment-url";
import { API_ENDPOINTS } from "@/lib/constants";
import {
  formatNoticeAudienceDetails,
  formatNoticeAudienceLabel,
} from "@/lib/notice-targeting";
import type { NoticeItem, PublishNoticeResponse } from "@/lib/types/notice";
import type { AttachmentItem } from "@/lib/types/attachment";
import {
  formatFileSize,
  isPdfAttachment,
} from "@/lib/types/attachment";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import {
  PageHeader,
  PageSection,
  PageShell,
} from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import {
  AlertTriangle,
  ArrowLeft,
  Bell,
  Eye,
  FileText,
  ImageIcon,
  Loader2,
  Send,
} from "lucide-react";

export default function NoticePreviewPage(): React.ReactElement {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const noticeId = params?.id ?? "";

  const [notice, setNotice] = React.useState<NoticeItem | null>(null);
  const [attachments, setAttachments] = React.useState<AttachmentItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState("");
  const [publishError, setPublishError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");
  const [isPublishing, setIsPublishing] = React.useState(false);

  const fetchPreview = React.useCallback(async () => {
    if (!noticeId) return;
    setIsLoading(true);
    setLoadError("");

    try {
      const [noticeData, attachmentItems] = await Promise.all([
        apiGet<NoticeItem>(`${API_ENDPOINTS.notices}/${noticeId}`),
        apiGet<AttachmentItem[]>(
          `${API_ENDPOINTS.attachments}?entityId=${noticeId}&entityType=notice`
        ),
      ]);
      setNotice(noticeData);
      setAttachments(attachmentItems);
    } catch (err) {
      setLoadError(
        err instanceof ApiError
          ? err.message
          : "Failed to load notice preview."
      );
    } finally {
      setIsLoading(false);
    }
  }, [noticeId]);

  React.useEffect(() => {
    void fetchPreview();
  }, [fetchPreview]);

  const hasUnresolvedAttachments = attachments.some(
    (attachment) =>
      attachment.status === "Pending" || attachment.status === "ScanFailed"
  );

  const handlePublish = async (): Promise<void> => {
    if (!notice) return;
    setPublishError("");
    setSuccessMessage("");
    setIsPublishing(true);

    try {
      const response = await apiPut<PublishNoticeResponse>(
        `${API_ENDPOINTS.notices}/${notice.noticeId}/publish`,
        { noticeId: notice.noticeId }
      );
      setSuccessMessage(response.message);
      // Refresh once to confirm published state then redirect back so the
      // admin sees the notice move from Drafts to Published.
      router.push("/admin/notices");
    } catch (err) {
      setPublishError(
        err instanceof ApiError ? err.message : "Failed to publish notice."
      );
    } finally {
      setIsPublishing(false);
    }
  };

  const formatDate = (value: string): string =>
    new Date(value).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });

  if (isLoading) {
    return (
      <PageShell>
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      </PageShell>
    );
  }

  if (loadError || !notice) {
    return (
      <PageShell>
        <ErrorState
          title="Unable to load preview"
          message={loadError || "Notice not found."}
          onRetry={fetchPreview}
        />
        <div className="mt-4">
          <Button asChild variant="outline">
            <Link href="/admin/notices">
              <ArrowLeft className="h-4 w-4" />
              Back to notices
            </Link>
          </Button>
        </div>
      </PageShell>
    );
  }

  const canPublish = notice.capabilities.canPublishDraft && !notice.isPublished;

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Preview notice"
        description="Review exactly what recipients will see before publishing. Attachments cannot be changed after publish."
        icon={<Eye className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="outline" size="sm">
              <Link href="/admin/notices">
                <ArrowLeft className="h-4 w-4" />
                Back to notices
              </Link>
            </Button>
            {canPublish && (
              <Button
                size="sm"
                onClick={() => void handlePublish()}
                disabled={isPublishing}
              >
                {isPublishing ? (
                  <Spinner size="sm" />
                ) : (
                  <>
                    <Send className="h-4 w-4" />
                    Publish
                  </>
                )}
              </Button>
            )}
          </div>
        )}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}

      {publishError && (
        <StatusBanner variant="error">{publishError}</StatusBanner>
      )}

      {notice.isPublished && (
        <StatusBanner variant="success">
          This notice is already published. Attachments and content are now immutable.
        </StatusBanner>
      )}

      {hasUnresolvedAttachments && !notice.isPublished && (
        <StatusBanner variant="warning">
          Some attachments have not completed virus scanning or failed to scan.
          Unresolved files are not previewable now, may not appear to recipients
          after publish, and cannot be changed once the notice is published.
        </StatusBanner>
      )}

      <PageSection className="space-y-4">
        <Card>
          <CardHeader className="space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <CardTitle className="text-2xl">{notice.title}</CardTitle>
              <div className="flex flex-wrap items-center gap-2">
                <Badge
                  variant="outline"
                  className={
                    notice.isPublished
                      ? "border-transparent bg-[linear-gradient(135deg,rgb(var(--warning)),rgb(var(--primary)))] text-primary-foreground"
                      : "bg-[rgb(var(--muted))] text-[rgb(var(--muted-foreground))]"
                  }
                >
                  {notice.isPublished ? "Published" : "Draft"}
                </Badge>
                <Badge
                  variant="outline"
                  className="border-[rgb(var(--success)/0.3)] bg-[rgb(var(--success)/0.15)] text-[rgb(var(--success))]"
                >
                  {formatNoticeAudienceLabel(notice)}
                </Badge>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="whitespace-pre-wrap text-sm text-foreground">
              {notice.body}
            </p>
            {formatNoticeAudienceDetails(notice) && (
              <p className="text-xs text-muted-foreground">
                {formatNoticeAudienceDetails(notice)}
              </p>
            )}
            <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground">
              <span>Created: {formatDate(notice.createdAt)}</span>
              {notice.publishedAt && (
                <span>Published: {formatDate(notice.publishedAt)}</span>
              )}
              {notice.expiresAt && (
                <span>Expires: {formatDate(notice.expiresAt)}</span>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Bell className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
              Attachments ({attachments.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            {attachments.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                This notice has no attachments.
              </p>
            ) : (
              <div className="space-y-4">
                {attachments.map((attachment) => (
                  <AttachmentPreviewRow
                    key={attachment.id}
                    attachment={attachment}
                  />
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </PageSection>
    </PageShell>
  );
}

function AttachmentPreviewRow({
  attachment,
}: {
  attachment: AttachmentItem;
}): React.ReactElement {
  const isAvailable = attachment.status === "Available";
  const isImage = attachment.contentType.startsWith("image/");
  const isPdf = isPdfAttachment(attachment.contentType);
  const viewUrl =
    isAvailable && attachment.downloadUrl
      ? normalizeAttachmentViewUrl(attachment.downloadUrl)
      : "";
  const downloadUrl =
    isAvailable && attachment.downloadUrl
      ? buildAttachmentDownloadUrl(attachment.downloadUrl)
      : "";

  return (
    <div className="rounded-[22px] border border-border/70 bg-card/72 p-4 shadow-[0_14px_30px_-28px_rgba(15,40,69,0.4)] dark:bg-card/86">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-secondary/70">
            {isImage ? (
              <ImageIcon
                className="h-4 w-4 text-emerald-600"
                aria-hidden="true"
              />
            ) : (
              <FileText className="h-4 w-4 text-rose-600" aria-hidden="true" />
            )}
          </div>
          <div className="min-w-0">
            <p className="truncate text-sm font-medium text-foreground">
              {attachment.fileName}
            </p>
            <p className="text-xs text-muted-foreground">
              {formatFileSize(attachment.sizeBytes)} •{" "}
              {attachment.contentType}
            </p>
          </div>
        </div>
        <StatusBadge status={attachment.status} />
      </div>

      {isAvailable && viewUrl && isImage && (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={viewUrl}
          alt={attachment.fileName}
          className="mt-4 max-h-[540px] w-full rounded-[18px] border border-border/60 object-contain bg-muted/40"
        />
      )}

      {isAvailable && viewUrl && isPdf && (
        <div className="mt-4 space-y-2">
          {/*
            Using <iframe> (not <object>) because middleware's CSP sets
            object-src 'none'. frame-src is extended in middleware.ts to
            allow the R2 media origin so the final redirect target is
            embeddable. sandbox="" blocks script execution inside the
            frame; browser PDF viewers don't need scripts.
          */}
          <iframe
            src={viewUrl}
            title={attachment.fileName}
            sandbox=""
            className="h-[640px] w-full rounded-[18px] border border-border/60 bg-muted/40"
          />
        </div>
      )}

      {isAvailable && viewUrl && (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          {(isImage || isPdf) && (
            <a
              href={viewUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex text-xs font-medium text-primary underline-offset-2 hover:underline"
            >
              Open in new tab
            </a>
          )}
          <a
            href={downloadUrl || viewUrl}
            className="inline-flex text-xs font-medium text-primary underline-offset-2 hover:underline"
          >
            Download file
          </a>
        </div>
      )}
    </div>
  );
}

function StatusBadge({
  status,
}: {
  status: AttachmentItem["status"];
}): React.ReactElement | null {
  if (status === "Available") {
    return (
      <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-[rgb(var(--success)/0.15)] px-2 py-0.5 text-xs font-medium text-[rgb(var(--success))]">
        Available
      </span>
    );
  }

  if (status === "Pending") {
    return (
      <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
        <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />
        Scanning…
      </span>
    );
  }

  if (status === "ScanFailed") {
    return (
      <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-destructive/10 px-2 py-0.5 text-xs font-medium text-destructive">
        <AlertTriangle className="h-3 w-3" aria-hidden="true" />
        Scan failed
      </span>
    );
  }

  return (
    <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-destructive/10 px-2 py-0.5 text-xs font-medium text-destructive">
      <AlertTriangle className="h-3 w-3" aria-hidden="true" />
      Blocked
    </span>
  );
}
