export interface NoticeTargetClassItem {
  classId: string;
  className: string;
  section: string;
  academicYear: string;
}

// Flags projected by the API so the admin UI matches the server's
// per-user authorization rules without trial-and-error requests. See
// NoticeCapabilities on the API side.
export interface NoticeCapabilities {
  canEditDraft: boolean;
  canManageDraftAttachments: boolean;
  canPreviewDraft: boolean;
  canPublishDraft: boolean;
}

export interface NoticeItem {
  noticeId: string;
  title: string;
  body: string;
  targetAudience: "All" | "Class" | "Section";
  targetClasses: NoticeTargetClassItem[];
  isPublished: boolean;
  publishedAt: string | null;
  expiresAt: string | null;
  createdAt: string;
  capabilities: NoticeCapabilities;
  attachmentCount: number;
}

export interface CreateNoticeRequest {
  title: string;
  body: string;
  targetAudience: "All" | "Class" | "Section";
  targetClassIds: string[] | null;
  expiresAt: string | null;
}

export interface CreateNoticeResponse {
  noticeId: string;
  message: string;
}

export interface UpdateNoticeRequest {
  title: string;
  body: string;
  targetAudience: "All" | "Class" | "Section";
  targetClassIds: string[] | null;
  expiresAt: string | null;
}

export interface UpdateNoticeResponse {
  noticeId: string;
  message: string;
}

export interface PublishNoticeResponse {
  message: string;
}
