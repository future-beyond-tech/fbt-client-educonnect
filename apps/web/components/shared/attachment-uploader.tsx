"use client";

import * as React from "react";
import { ApiError, apiPost, apiDelete } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { FileUp, X, FileText, ImageIcon, AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import type {
  RequestUploadUrlRequest,
  RequestUploadUrlResponse,
  AttachFileRequest,
  AttachFileResponse,
  DeleteAttachmentResponse,
} from "@/lib/types/attachment";
import {
  isAllowedContentType,
  formatFileSize,
  MAX_FILE_SIZE_BYTES,
  MAX_ATTACHMENTS_PER_ENTITY,
} from "@/lib/types/attachment";

export interface UploadedFile {
  attachmentId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

interface FileInProgress {
  id: string;
  file: File;
  progress: number;
  status: "uploading" | "attaching" | "done" | "error";
  error?: string;
  attachmentId?: string;
}

interface AttachmentUploaderProps {
  entityId: string;
  entityType: "homework" | "notice";
  existingAttachments?: UploadedFile[];
  onAttachmentsChange: (attachments: UploadedFile[]) => void;
  disabled?: boolean;
}

export function AttachmentUploader({
  entityId,
  entityType,
  existingAttachments = [],
  onAttachmentsChange,
  disabled = false,
}: AttachmentUploaderProps): React.ReactElement {
  const [filesInProgress, setFilesInProgress] = React.useState<
    FileInProgress[]
  >([]);
  const [isDragOver, setIsDragOver] = React.useState(false);
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  const totalAttached = existingAttachments.length;
  const canAddMore = totalAttached < MAX_ATTACHMENTS_PER_ENTITY;

  const handleFiles = React.useCallback(
    async (files: FileList | File[]) => {
      const fileArray = Array.from(files);
      const remaining = MAX_ATTACHMENTS_PER_ENTITY - totalAttached;

      if (fileArray.length > remaining) {
        fileArray.splice(remaining);
      }

      for (const file of fileArray) {
        if (!isAllowedContentType(file.type)) {
          setFilesInProgress((prev) => [
            ...prev,
            {
              id: crypto.randomUUID(),
              file,
              progress: 0,
              status: "error",
              error: "Unsupported file type. Use JPEG, PNG, WebP, or PDF.",
            },
          ]);
          continue;
        }

        if (file.size > MAX_FILE_SIZE_BYTES) {
          setFilesInProgress((prev) => [
            ...prev,
            {
              id: crypto.randomUUID(),
              file,
              progress: 0,
              status: "error",
              error: `File too large (${formatFileSize(file.size)}). Max 10MB.`,
            },
          ]);
          continue;
        }

        const trackingId = crypto.randomUUID();

        setFilesInProgress((prev) => [
          ...prev,
          { id: trackingId, file, progress: 0, status: "uploading" },
        ]);

        try {
          // Step 1: Request presigned upload URL
          const body: RequestUploadUrlRequest = {
            fileName: file.name,
            contentType: file.type,
            sizeBytes: file.size,
          };

          const uploadUrlResponse = await apiPost<RequestUploadUrlResponse>(
            API_ENDPOINTS.attachmentsRequestUpload,
            body
          );

          setFilesInProgress((prev) =>
            prev.map((f) =>
              f.id === trackingId
                ? {
                    ...f,
                    progress: 30,
                    attachmentId: uploadUrlResponse.attachmentId,
                  }
                : f
            )
          );

          // Step 2: Upload file directly to S3 via presigned URL
          const uploadResponse = await fetch(uploadUrlResponse.uploadUrl, {
            method: "PUT",
            headers: { "Content-Type": file.type },
            body: file,
          });

          if (!uploadResponse.ok) {
            throw new Error("Failed to upload file to storage.");
          }

          setFilesInProgress((prev) =>
            prev.map((f) =>
              f.id === trackingId
                ? { ...f, progress: 70, status: "attaching" }
                : f
            )
          );

          // Step 3: Attach to entity
          const attachBody: AttachFileRequest = {
            attachmentId: uploadUrlResponse.attachmentId,
            entityId,
            entityType,
          };

          await apiPost<AttachFileResponse>(
            API_ENDPOINTS.attachmentsAttach,
            attachBody
          );

          setFilesInProgress((prev) =>
            prev.map((f) =>
              f.id === trackingId ? { ...f, progress: 100, status: "done" } : f
            )
          );

          // Add to parent's attached list
          const newAttachment: UploadedFile = {
            attachmentId: uploadUrlResponse.attachmentId,
            fileName: file.name,
            contentType: file.type,
            sizeBytes: file.size,
          };

          onAttachmentsChange([...existingAttachments, newAttachment]);

          // Remove from progress list after a short delay
          setTimeout(() => {
            setFilesInProgress((prev) =>
              prev.filter((f) => f.id !== trackingId)
            );
          }, 1500);
        } catch (err) {
          const errorMessage =
            err instanceof ApiError
              ? err.message
              : err instanceof Error
                ? err.message
                : "Upload failed.";

          setFilesInProgress((prev) =>
            prev.map((f) =>
              f.id === trackingId
                ? { ...f, status: "error", error: errorMessage }
                : f
            )
          );
        }
      }
    },
    [entityId, entityType, existingAttachments, onAttachmentsChange, totalAttached]
  );

  const handleDrop = React.useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      if (disabled || !canAddMore) return;
      void handleFiles(e.dataTransfer.files);
    },
    [disabled, canAddMore, handleFiles]
  );

  const handleDragOver = (e: React.DragEvent): void => {
    e.preventDefault();
    if (!disabled && canAddMore) {
      setIsDragOver(true);
    }
  };

  const handleDragLeave = (e: React.DragEvent): void => {
    e.preventDefault();
    setIsDragOver(false);
  };

  const handleBrowse = (): void => {
    fileInputRef.current?.click();
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>): void => {
    if (e.target.files && e.target.files.length > 0) {
      void handleFiles(e.target.files);
      e.target.value = "";
    }
  };

  const handleRemoveAttachment = async (
    attachmentId: string
  ): Promise<void> => {
    try {
      await apiDelete<DeleteAttachmentResponse>(
        `${API_ENDPOINTS.attachments}/${attachmentId}`
      );
      onAttachmentsChange(
        existingAttachments.filter((a) => a.attachmentId !== attachmentId)
      );
    } catch {
      // Silently fail — UI stays consistent
    }
  };

  const handleDismissError = (trackingId: string): void => {
    setFilesInProgress((prev) => prev.filter((f) => f.id !== trackingId));
  };

  const getFileIcon = (contentType: string): React.ReactNode => {
    if (contentType === "application/pdf") {
      return <FileText className="h-4 w-4 text-red-500" aria-hidden="true" />;
    }
    return <ImageIcon className="h-4 w-4 text-blue-500" aria-hidden="true" />;
  };

  return (
    <div className="space-y-3">
      {/* Drop zone */}
      <div
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        className={cn(
          "flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 transition-colors",
          isDragOver
            ? "border-primary bg-primary/5"
            : "border-border bg-muted/30",
          (disabled || !canAddMore) && "cursor-not-allowed opacity-50"
        )}
      >
        <FileUp
          className="mb-2 h-6 w-6 text-muted-foreground"
          aria-hidden="true"
        />
        <p className="text-sm text-muted-foreground">
          {canAddMore
            ? "Drag & drop files here, or"
            : `Maximum ${MAX_ATTACHMENTS_PER_ENTITY} files reached`}
        </p>
        {canAddMore && (
          <Button
            type="button"
            variant="link"
            size="sm"
            onClick={handleBrowse}
            disabled={disabled}
            className="mt-1"
          >
            Browse files
          </Button>
        )}
        <p className="mt-1 text-xs text-muted-foreground">
          JPEG, PNG, WebP, or PDF — max 10MB each
        </p>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp,application/pdf"
          multiple
          onChange={handleInputChange}
          className="hidden"
          disabled={disabled || !canAddMore}
        />
      </div>

      {/* Upload progress */}
      {filesInProgress.map((f) => (
        <div
          key={f.id}
          className={cn(
            "flex items-center gap-3 rounded-md border p-3",
            f.status === "error" && "border-destructive/50 bg-destructive/5"
          )}
        >
          {f.status === "error" ? (
            <AlertCircle
              className="h-4 w-4 shrink-0 text-destructive"
              aria-hidden="true"
            />
          ) : (
            getFileIcon(f.file.type)
          )}
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{f.file.name}</p>
            {f.status === "error" ? (
              <p className="text-xs text-destructive">{f.error}</p>
            ) : (
              <div className="mt-1 h-1.5 w-full rounded-full bg-muted">
                <div
                  className="h-full rounded-full bg-primary transition-all duration-300"
                  style={{ width: `${f.progress}%` }}
                />
              </div>
            )}
          </div>
          {f.status === "error" && (
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-7 w-7 shrink-0"
              onClick={() => handleDismissError(f.id)}
            >
              <X className="h-3 w-3" />
            </Button>
          )}
        </div>
      ))}

      {/* Attached files */}
      {existingAttachments.length > 0 && (
        <div className="space-y-2">
          <p className="text-xs font-medium text-muted-foreground">
            {existingAttachments.length} / {MAX_ATTACHMENTS_PER_ENTITY} files
            attached
          </p>
          {existingAttachments.map((a) => (
            <div
              key={a.attachmentId}
              className="flex items-center gap-3 rounded-md border p-3"
            >
              {getFileIcon(a.contentType)}
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{a.fileName}</p>
                <p className="text-xs text-muted-foreground">
                  {formatFileSize(a.sizeBytes)}
                </p>
              </div>
              {!disabled && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 shrink-0 text-muted-foreground hover:text-destructive"
                  onClick={() => {
                    void handleRemoveAttachment(a.attachmentId);
                  }}
                  aria-label={`Remove ${a.fileName}`}
                >
                  <X className="h-3 w-3" />
                </Button>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
