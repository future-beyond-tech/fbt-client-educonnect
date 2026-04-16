"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Spinner } from "@/components/ui/spinner";
import { Download, FileText, ImageIcon, Paperclip } from "lucide-react";
import type { AttachmentEntityType, AttachmentItem } from "@/lib/types/attachment";
import {
  formatFileSize,
  isPdfAttachment,
  isWordAttachment,
} from "@/lib/types/attachment";

interface AttachmentListProps {
  entityId: string;
  entityType: AttachmentEntityType;
}

export function AttachmentList({
  entityId,
  entityType,
}: AttachmentListProps): React.ReactElement {
  const [attachments, setAttachments] = React.useState<AttachmentItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const fetchAttachments = React.useCallback(async () => {
    setIsLoading(true);
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
      setIsLoading(false);
    }
  }, [entityId, entityType]);

  React.useEffect(() => {
    void fetchAttachments();
  }, [fetchAttachments]);

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
        {attachments.map((attachment) => {
          const opensInNewTab = !isWordAttachment(attachment.contentType);

          return (
            <a
              key={attachment.id}
              href={attachment.downloadUrl}
              target={opensInNewTab ? "_blank" : undefined}
              rel={opensInNewTab ? "noopener noreferrer" : undefined}
              className="flex items-center gap-3 rounded-[20px] border border-border/70 bg-card/72 p-3 shadow-[0_14px_30px_-28px_rgba(15,40,69,0.4)] transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card/92 dark:bg-card/86"
              aria-label={`Download ${attachment.fileName}`}
            >
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
              <Download
                className="h-4 w-4 shrink-0 text-muted-foreground"
                aria-hidden="true"
              />
            </a>
          );
        })}
      </div>
    </div>
  );
}
