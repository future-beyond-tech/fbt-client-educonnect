"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Spinner } from "@/components/ui/spinner";
import {
  AlertTriangle,
  Download,
  FileText,
  ImageIcon,
  Loader2,
  Paperclip,
} from "lucide-react";
import type {
  AttachmentEntityType,
  AttachmentItem,
  AttachmentStatus,
} from "@/lib/types/attachment";
import {
  formatFileSize,
  isPdfAttachment,
  isWordAttachment,
} from "@/lib/types/attachment";

interface AttachmentListProps {
  entityId: string;
  entityType: AttachmentEntityType;
}

// Re-poll every 5s while any row is Pending. Cap total polling to keep
// the network footprint bounded — after the deadline the user has to
// reload the page (the badge stays "Scanning…" so they see it).
const POLL_INTERVAL_MS = 5_000;
const POLL_DEADLINE_MS = 2 * 60 * 1_000;

export function AttachmentList({
  entityId,
  entityType,
}: AttachmentListProps): React.ReactElement {
  const [attachments, setAttachments] = React.useState<AttachmentItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const fetchAttachments = React.useCallback(
    async (showLoader: boolean) => {
      if (showLoader) {
        setIsLoading(true);
      }
      setError("");

      try {
        const data = await apiGet<AttachmentItem[]>(
          `${API_ENDPOINTS.attachments}?entityId=${entityId}&entityType=${entityType}`
        );
        setAttachments(data);
      } catch (err) {
        setError(
          err instanceof ApiError ? err.message : "Failed to load attachments."
        );
      } finally {
        if (showLoader) {
          setIsLoading(false);
        }
      }
    },
    [entityId, entityType]
  );

  React.useEffect(() => {
    void fetchAttachments(true);
  }, [fetchAttachments]);

  // Background poll while any attachment is still scanning. Stops once
  // every row resolves or the polling deadline passes — whichever comes
  // first. The interval is recreated whenever attachments change, which
  // is intentional: it lets the loop tear itself down the moment all
  // Pending rows clear.
  React.useEffect(() => {
    const hasPending = attachments.some((a) => a.status === "Pending");
    if (!hasPending) return;

    const startedAt = Date.now();
    const handle = window.setInterval(() => {
      if (Date.now() - startedAt > POLL_DEADLINE_MS) {
        window.clearInterval(handle);
        return;
      }
      void fetchAttachments(false);
    }, POLL_INTERVAL_MS);

    return () => window.clearInterval(handle);
  }, [attachments, fetchAttachments]);

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-2">
        <Spinner size="sm" />
        <span className="text-sm text-muted-foreground">
          Loading attachments...
        </span>
      </div>
    );
  }

  if (error) {
    return <p className="text-sm text-destructive">{error}</p>;
  }

  if (attachments.length === 0) {
    return (
      <div className="py-2">
        <p className="text-sm text-muted-foreground">No attachments.</p>
      </div>
    );
  }

  const getFileIcon = (contentType: string): React.ReactNode => {
    if (isWordAttachment(contentType)) {
      return <FileText className="h-4 w-4 text-sky-600" aria-hidden="true" />;
    }

    if (isPdfAttachment(contentType)) {
      return <FileText className="h-4 w-4 text-rose-600" aria-hidden="true" />;
    }

    return <ImageIcon className="h-4 w-4 text-emerald-600" aria-hidden="true" />;
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-1.5">
        <Paperclip
          className="h-4 w-4 text-muted-foreground"
          aria-hidden="true"
        />
        <p className="text-sm font-medium text-muted-foreground">
          {attachments.length} attachment{attachments.length !== 1 ? "s" : ""}
        </p>
      </div>
      <div className="space-y-1.5">
        {attachments.map((attachment) => (
          <AttachmentRow
            key={attachment.id}
            attachment={attachment}
            getFileIcon={getFileIcon}
          />
        ))}
      </div>
    </div>
  );
}

interface AttachmentRowProps {
  attachment: AttachmentItem;
  getFileIcon: (contentType: string) => React.ReactNode;
}

function AttachmentRow({
  attachment,
  getFileIcon,
}: AttachmentRowProps): React.ReactElement {
  const isAvailable = attachment.status === "Available";
  const opensInNewTab = !isWordAttachment(attachment.contentType);
  const baseClass =
    "flex items-center gap-3 rounded-[20px] border border-border/70 bg-card/72 p-3 shadow-[0_14px_30px_-28px_rgba(15,40,69,0.4)] dark:bg-card/86";

  const body = (
    <>
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-secondary/70">
        {getFileIcon(attachment.contentType)}
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-foreground">
          {attachment.fileName}
        </p>
        <p className="text-xs text-muted-foreground">
          {formatFileSize(attachment.sizeBytes)}
        </p>
      </div>
      <StatusBadge status={attachment.status} />
      {isAvailable && attachment.downloadUrl ? (
        <Download
          className="h-4 w-4 shrink-0 text-muted-foreground"
          aria-hidden="true"
        />
      ) : null}
    </>
  );

  if (isAvailable && attachment.downloadUrl) {
    return (
      <a
        href={attachment.downloadUrl}
        target={opensInNewTab ? "_blank" : undefined}
        rel={opensInNewTab ? "noopener noreferrer" : undefined}
        className={`${baseClass} transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card/92`}
        aria-label={`Download ${attachment.fileName}`}
      >
        {body}
      </a>
    );
  }

  return (
    <div className={baseClass} aria-label={attachment.fileName}>
      {body}
    </div>
  );
}

function StatusBadge({ status }: { status: AttachmentStatus }): React.ReactElement | null {
  if (status === "Available") return null;

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

  // Infected rows are filtered out server-side for every role today, so
  // this branch is unreachable. Render a generic blocked badge as a
  // belt-and-braces fallback in case the server ever changes.
  return (
    <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-destructive/10 px-2 py-0.5 text-xs font-medium text-destructive">
      <AlertTriangle className="h-3 w-3" aria-hidden="true" />
      Blocked
    </span>
  );
}
