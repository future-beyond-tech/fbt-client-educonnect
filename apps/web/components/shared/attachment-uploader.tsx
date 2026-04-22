"use client";

import * as React from "react";
import { useDropzone, type FileRejection } from "react-dropzone";
import { AlertCircle, CheckCircle2, FileText, ImageIcon, UploadCloud, X } from "lucide-react";
import { ApiError, apiDelete, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type {
  AttachFileRequest,
  AttachFileResponse,
  AttachmentEntityType,
  DeleteAttachmentResponse,
  RequestUploadUrlRequest,
  RequestUploadUrlResponse,
} from "@/lib/types/attachment";
import {
  MAX_ATTACHMENTS_PER_ENTITY,
  MAX_FILE_SIZE_BYTES,
  formatFileSize,
  getAcceptedFiles,
  getAttachmentEmptyLabel,
  getAttachmentHelperText,
  getAttachmentTypeRejectionMessage,
  isAllowedContentType,
  isPdfAttachment,
  isWordAttachment,
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
}

interface AttachmentUploaderProps {
  entityId: string;
  entityType: AttachmentEntityType;
  existingAttachments?: UploadedFile[];
  onAttachmentsChange: (attachments: UploadedFile[]) => void;
  disabled?: boolean;
}

function uploadToPresignedUrl(
  uploadUrl: string,
  file: File,
  onProgress: (progress: number) => void
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", uploadUrl);
    xhr.setRequestHeader("Content-Type", file.type);

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable) return;
      onProgress(Math.min(95, Math.round((event.loaded / event.total) * 100)));
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        onProgress(100);
        resolve();
        return;
      }

      reject(new Error("Failed to upload file to storage."));
    };

    xhr.onerror = () => reject(new Error("Network error during upload."));
    xhr.send(file);
  });
}

export function AttachmentUploader({
  entityId,
  entityType,
  existingAttachments = [],
  onAttachmentsChange,
  disabled = false,
}: AttachmentUploaderProps): React.ReactElement {
  const attachmentsRef = React.useRef(existingAttachments);
  const [filesInProgress, setFilesInProgress] = React.useState<FileInProgress[]>(
    []
  );
  const [confirmingDeleteId, setConfirmingDeleteId] = React.useState<string | null>(
    null
  );
  const [deleteError, setDeleteError] = React.useState<string>("");

  React.useEffect(() => {
    attachmentsRef.current = existingAttachments;
  }, [existingAttachments]);

  const totalAttached = existingAttachments.length;
  const canAddMore = totalAttached < MAX_ATTACHMENTS_PER_ENTITY;
  const remainingSlots = Math.max(0, MAX_ATTACHMENTS_PER_ENTITY - totalAttached);

  const updateProgressItem = React.useCallback(
    (trackingId: string, next: Partial<FileInProgress>): void => {
      setFilesInProgress((prev) =>
        prev.map((file) => (file.id === trackingId ? { ...file, ...next } : file))
      );
    },
    []
  );

  const addFileError = React.useCallback((file: File, error: string): void => {
    setFilesInProgress((prev) => [
      ...prev,
      {
        id: crypto.randomUUID(),
        file,
        progress: 0,
        status: "error",
        error,
      },
    ]);
  }, []);

  const uploadFile = React.useCallback(
    async (file: File): Promise<void> => {
      const trackingId = crypto.randomUUID();

      setFilesInProgress((prev) => [
        ...prev,
        {
          id: trackingId,
          file,
          progress: 0,
          status: "uploading",
        },
      ]);

      try {
        const body: RequestUploadUrlRequest = {
          fileName: file.name,
          contentType: file.type,
          sizeBytes: file.size,
          entityType,
        };

        const uploadUrlResponse = await apiPost<RequestUploadUrlResponse>(
          API_ENDPOINTS.attachmentsRequestUploadV2,
          body
        );

        updateProgressItem(trackingId, { progress: 10 });

        await uploadToPresignedUrl(uploadUrlResponse.uploadUrl, file, (progress) => {
          updateProgressItem(trackingId, { progress });
        });

        updateProgressItem(trackingId, { progress: 100, status: "attaching" });

        const attachBody: AttachFileRequest = {
          attachmentId: uploadUrlResponse.attachmentId,
          entityId,
          entityType,
        };

        await apiPost<AttachFileResponse>(API_ENDPOINTS.attachmentsAttach, attachBody);

        updateProgressItem(trackingId, { progress: 100, status: "done" });

        const nextAttachments = [
          ...attachmentsRef.current,
          {
            attachmentId: uploadUrlResponse.attachmentId,
            fileName: file.name,
            contentType: file.type,
            sizeBytes: file.size,
          },
        ];

        attachmentsRef.current = nextAttachments;
        onAttachmentsChange(nextAttachments);

        window.setTimeout(() => {
          setFilesInProgress((prev) => prev.filter((item) => item.id !== trackingId));
        }, 1200);
      } catch (error) {
        const message =
          error instanceof ApiError
            ? error.message
            : error instanceof Error
              ? error.message
              : "Upload failed.";

        updateProgressItem(trackingId, {
          status: "error",
          error: message,
        });
      }
    },
    [entityId, entityType, onAttachmentsChange, updateProgressItem]
  );

  const handleAcceptedFiles = React.useCallback(
    async (acceptedFiles: File[]) => {
      setDeleteError("");

      const remaining = MAX_ATTACHMENTS_PER_ENTITY - existingAttachments.length;
      const filesToUpload = acceptedFiles.slice(0, remaining);

      if (acceptedFiles.length > remaining) {
        acceptedFiles.slice(remaining).forEach((file) => {
          addFileError(
            file,
            `Maximum ${MAX_ATTACHMENTS_PER_ENTITY} attachments allowed per ${entityType}.`
          );
        });
      }

      for (const file of filesToUpload) {
        if (!isAllowedContentType(entityType, file.type)) {
          addFileError(file, getAttachmentTypeRejectionMessage(entityType));
          continue;
        }

        if (file.size > MAX_FILE_SIZE_BYTES) {
          addFileError(
            file,
            `File too large (${formatFileSize(file.size)}). Max 10MB.`
          );
          continue;
        }

        await uploadFile(file);
      }
    },
    [addFileError, entityType, existingAttachments.length, uploadFile]
  );

  const onDropRejected = React.useCallback(
    (rejections: FileRejection[]) => {
      rejections.forEach(({ file, errors }) => {
        const firstError = errors[0];

        if (firstError?.code === "too-many-files") {
          addFileError(
            file,
            `Maximum ${MAX_ATTACHMENTS_PER_ENTITY} attachments allowed per ${entityType}.`
          );
          return;
        }

        if (firstError?.code === "file-too-large") {
          addFileError(
            file,
            `File too large (${formatFileSize(file.size)}). Max 10MB.`
          );
          return;
        }

        addFileError(file, getAttachmentTypeRejectionMessage(entityType));
      });
    },
    [addFileError, entityType]
  );

  const { getRootProps, getInputProps, isDragActive, open } = useDropzone({
    accept: getAcceptedFiles(entityType),
    disabled: disabled || !canAddMore,
    maxFiles: remainingSlots,
    maxSize: MAX_FILE_SIZE_BYTES,
    multiple: true,
    noClick: true,
    noKeyboard: true,
    onDropAccepted: (acceptedFiles) => {
      void handleAcceptedFiles(acceptedFiles);
    },
    onDropRejected,
  });

  const handleRemoveAttachment = async (attachmentId: string): Promise<void> => {
    setDeleteError("");

    try {
      await apiDelete<DeleteAttachmentResponse>(
        `${API_ENDPOINTS.attachments}/${attachmentId}`
      );

      const nextAttachments = attachmentsRef.current.filter(
        (attachment) => attachment.attachmentId !== attachmentId
      );

      attachmentsRef.current = nextAttachments;
      onAttachmentsChange(nextAttachments);
      setConfirmingDeleteId(null);
    } catch (error) {
      setDeleteError(
        error instanceof ApiError ? error.message : "Failed to delete attachment."
      );
    }
  };

  const dismissProgressItem = (trackingId: string): void => {
    setFilesInProgress((prev) => prev.filter((file) => file.id !== trackingId));
  };

  const getFileIcon = (contentType: string): React.ReactNode => {
    if (isWordAttachment(contentType)) {
      return <FileText className="h-5 w-5 text-sky-600" aria-hidden="true" />;
    }

    if (isPdfAttachment(contentType)) {
      return <FileText className="h-5 w-5 text-rose-600" aria-hidden="true" />;
    }

    return <ImageIcon className="h-5 w-5 text-emerald-600" aria-hidden="true" />;
  };

  return (
    <div className="space-y-4">
      <div
        {...getRootProps({
          className: cn(
            "rounded-[24px] border-2 border-dashed p-5 text-center transition-colors",
            isDragActive
              ? "border-primary bg-primary/5"
              : "border-border bg-muted/30",
            (disabled || !canAddMore) && "cursor-not-allowed opacity-60"
          ),
          role: "button",
          tabIndex: disabled || !canAddMore ? -1 : 0,
          "aria-label": `Upload ${entityType} attachments`,
          onKeyDown: (event: React.KeyboardEvent<HTMLDivElement>) => {
            if (disabled || !canAddMore) return;
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              open();
            }
          },
        })}
      >
        <input {...getInputProps()} />
        <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-card/80 shadow-[0_14px_32px_-26px_rgba(15,40,69,0.35)]">
          <UploadCloud className="h-6 w-6 text-primary" aria-hidden="true" />
        </div>
        <p className="text-sm font-medium text-foreground">
          {canAddMore
            ? getAttachmentEmptyLabel(entityType)
            : `Maximum ${MAX_ATTACHMENTS_PER_ENTITY} attachments reached`}
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          {getAttachmentHelperText(entityType)}
        </p>
        {canAddMore && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={open}
            disabled={disabled}
            className="mt-4"
          >
            Browse files
          </Button>
        )}
      </div>

      {(filesInProgress.length > 0 || deleteError) && (
        <div className="space-y-3">
          {deleteError ? (
            <div className="rounded-[20px] border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
              {deleteError}
            </div>
          ) : null}

          {filesInProgress.map((file) => (
            <div
              key={file.id}
              className={cn(
                "flex items-center gap-3 rounded-[22px] border border-border/70 bg-card/82 p-3 shadow-[0_14px_30px_-28px_rgba(15,40,69,0.35)]",
                file.status === "error" && "border-destructive/40 bg-destructive/10"
              )}
            >
              <div className="shrink-0">
                {file.status === "done" ? (
                  <CheckCircle2
                    className="h-5 w-5 text-emerald-600"
                    aria-hidden="true"
                  />
                ) : file.status === "error" ? (
                  <AlertCircle
                    className="h-5 w-5 text-destructive"
                    aria-hidden="true"
                  />
                ) : (
                  getFileIcon(file.file.type)
                )}
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-foreground">
                  {file.file.name}
                </p>
                <p className="text-xs text-muted-foreground">
                  {formatFileSize(file.file.size)}
                </p>
                {file.status === "error" ? (
                  <p className="mt-1 text-xs text-destructive">{file.error}</p>
                ) : (
                  <div className="mt-2 h-1.5 w-full rounded-full bg-muted">
                    <div
                      className="h-full rounded-full bg-primary transition-all duration-300"
                      style={{ width: `${file.progress}%` }}
                    />
                  </div>
                )}
              </div>
              {file.status === "error" && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-11 w-11 shrink-0"
                  onClick={() => dismissProgressItem(file.id)}
                  aria-label={`Dismiss error for ${file.file.name}`}
                >
                  <X className="h-4 w-4" />
                </Button>
              )}
            </div>
          ))}
        </div>
      )}

      {existingAttachments.length > 0 && (
        <div className="space-y-3">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {existingAttachments.length} / {MAX_ATTACHMENTS_PER_ENTITY} attached
          </p>
          {existingAttachments.map((attachment) => (
            <div
              key={attachment.attachmentId}
              className="flex items-center gap-3 rounded-[22px] border border-border/70 bg-card/82 p-3 shadow-[0_14px_30px_-28px_rgba(15,40,69,0.35)]"
            >
              <div className="shrink-0">{getFileIcon(attachment.contentType)}</div>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-foreground">
                  {attachment.fileName}
                </p>
                <p className="text-xs text-muted-foreground">
                  {formatFileSize(attachment.sizeBytes)}
                </p>
              </div>

              {disabled ? null : confirmingDeleteId === attachment.attachmentId ? (
                <div className="flex items-center gap-2 rounded-full bg-destructive/10 px-3 py-1.5 text-xs font-medium text-destructive">
                  <span>Delete?</span>
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="h-7 rounded-full px-2 text-destructive hover:text-destructive"
                    onClick={() => {
                      void handleRemoveAttachment(attachment.attachmentId);
                    }}
                  >
                    Yes
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="h-7 rounded-full px-2"
                    onClick={() => setConfirmingDeleteId(null)}
                  >
                    No
                  </Button>
                </div>
              ) : (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="h-11 w-11 shrink-0 text-muted-foreground hover:text-destructive"
                  onClick={() => setConfirmingDeleteId(attachment.attachmentId)}
                  aria-label={`Delete ${attachment.fileName}`}
                >
                  <X className="h-4 w-4" />
                </Button>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
