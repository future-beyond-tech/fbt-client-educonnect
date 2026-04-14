export interface AttachmentItem {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  downloadUrl: string;
  uploadedAt: string;
}

export type AttachmentEntityType = "homework" | "notice";

export interface RequestUploadUrlRequest {
  fileName: string;
  contentType: string;
  sizeBytes: number;
  entityType: AttachmentEntityType;
}

export interface RequestUploadUrlResponse {
  uploadUrl: string;
  attachmentId: string;
}

export interface AttachFileRequest {
  attachmentId: string;
  entityId: string;
  entityType: AttachmentEntityType;
}

export interface AttachFileResponse {
  message: string;
}

export interface DeleteAttachmentResponse {
  message: string;
}

export const NOTICE_ALLOWED_CONTENT_TYPES = [
  "image/jpeg",
  "image/png",
  "image/webp",
  "application/pdf",
] as const;

export const HOMEWORK_ALLOWED_CONTENT_TYPES = [
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
] as const;

export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
export const MAX_ATTACHMENTS_PER_ENTITY = 5;

export const ATTACHMENT_ACCEPT = {
  notice: {
    "image/jpeg": [".jpg", ".jpeg"],
    "image/png": [".png"],
    "image/webp": [".webp"],
    "application/pdf": [".pdf"],
  },
  homework: {
    "application/pdf": [".pdf"],
    "application/msword": [".doc"],
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": [".docx"],
  },
} as const;

export type NoticeAllowedContentType =
  (typeof NOTICE_ALLOWED_CONTENT_TYPES)[number];

export type HomeworkAllowedContentType =
  (typeof HOMEWORK_ALLOWED_CONTENT_TYPES)[number];

export type AllowedContentType =
  | NoticeAllowedContentType
  | HomeworkAllowedContentType;

export function getAllowedContentTypes(
  entityType: AttachmentEntityType
): readonly string[] {
  return entityType === "homework"
    ? HOMEWORK_ALLOWED_CONTENT_TYPES
    : NOTICE_ALLOWED_CONTENT_TYPES;
}

export function getAcceptedFiles(
  entityType: AttachmentEntityType
): Record<string, readonly string[]> {
  return { ...ATTACHMENT_ACCEPT[entityType] };
}

export function isAllowedContentType(
  entityType: AttachmentEntityType,
  type: string
): type is AllowedContentType {
  return getAllowedContentTypes(entityType).includes(type);
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function isPdfAttachment(contentType: string): boolean {
  return contentType === "application/pdf";
}

export function isWordAttachment(contentType: string): boolean {
  return (
    contentType === "application/msword" ||
    contentType ===
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
  );
}

export function getAttachmentHelperText(
  entityType: AttachmentEntityType
): string {
  return entityType === "homework"
    ? "PDF, DOC, or DOCX — max 10MB each"
    : "JPEG, PNG, WebP, or PDF — max 10MB each";
}

export function getAttachmentEmptyLabel(
  entityType: AttachmentEntityType
): string {
  return entityType === "homework"
    ? "Drag and drop homework files here, or browse"
    : "Drag and drop notice files here, or browse";
}
