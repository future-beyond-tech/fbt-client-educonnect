export interface AttachmentItem {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  downloadUrl: string;
  uploadedAt: string;
}

export interface RequestUploadUrlRequest {
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface RequestUploadUrlResponse {
  uploadUrl: string;
  storageKey: string;
  attachmentId: string;
}

export interface AttachFileRequest {
  attachmentId: string;
  entityId: string;
  entityType: "homework" | "notice";
}

export interface AttachFileResponse {
  message: string;
}

export interface DeleteAttachmentResponse {
  message: string;
}

export const ALLOWED_CONTENT_TYPES = [
  "image/jpeg",
  "image/png",
  "image/webp",
  "application/pdf",
] as const;

export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
export const MAX_ATTACHMENTS_PER_ENTITY = 5;

export type AllowedContentType = (typeof ALLOWED_CONTENT_TYPES)[number];

export function isAllowedContentType(
  type: string
): type is AllowedContentType {
  return (ALLOWED_CONTENT_TYPES as readonly string[]).includes(type);
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
