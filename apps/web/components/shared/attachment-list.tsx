"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Spinner } from "@/components/ui/spinner";
import { FileText, ImageIcon, Download, Paperclip } from "lucide-react";
import { formatFileSize } from "@/lib/types/attachment";
import type { AttachmentItem } from "@/lib/types/attachment";

interface AttachmentListProps {
  entityId: string;
  entityType: "homework" | "notice";
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
        err instanceof ApiError
          ? err.message
          : "Failed to load attachments."
      );
    } finally {
      setIsLoading(false);
    }
  }, [entityId, entityType]);

  React.useEffect(() => {
    fetchAttachments();
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
    return (
      <p className="text-sm text-destructive">{error}</p>
    );
  }

  if (attachments.length === 0) {
    return <></>;
  }

  const getFileIcon = (contentType: string): React.ReactNode => {
    if (contentType === "application/pdf") {
      return <FileText className="h-4 w-4 text-red-500" aria-hidden="true" />;
    }
    return <ImageIcon className="h-4 w-4 text-blue-500" aria-hidden="true" />;
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
          <a
            key={attachment.id}
            href={attachment.downloadUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-3 rounded-md border p-3 transition-colors hover:bg-accent"
          >
            {getFileIcon(attachment.contentType)}
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
        ))}
      </div>
    </div>
  );
}
