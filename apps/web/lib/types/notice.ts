export interface NoticeTargetClassItem {
  classId: string;
  className: string;
  section: string;
  academicYear: string;
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

export interface PublishNoticeResponse {
  message: string;
}
