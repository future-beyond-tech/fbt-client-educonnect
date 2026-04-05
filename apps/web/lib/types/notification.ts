import type { PagedResult } from "./student";

export interface NotificationItem {
  id: string;
  type: "notice_published" | "homework_assigned" | "absence_marked";
  title: string;
  body: string | null;
  entityId: string | null;
  entityType: "notice" | "homework" | "attendance" | null;
  isRead: boolean;
  createdAt: string;
}

export type NotificationPagedResult = PagedResult<NotificationItem>;

export interface UnreadCountResponse {
  count: number;
}

export interface MarkReadResponse {
  message: string;
}

export interface MarkAllReadResponse {
  markedCount: number;
  message: string;
}
