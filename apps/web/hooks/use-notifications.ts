"use client";

import * as React from "react";
import { apiGet, apiPut, ApiError } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { useAuth } from "@/hooks/use-auth";
import type {
  NotificationItem,
  NotificationPagedResult,
  UnreadCountResponse,
  MarkReadResponse,
  MarkAllReadResponse,
} from "@/lib/types/notification";

const POLL_INTERVAL_MS = 60_000; // 60 seconds

interface UseNotificationsReturn {
  notifications: NotificationItem[];
  unreadCount: number;
  totalCount: number;
  totalPages: number;
  page: number;
  isLoading: boolean;
  error: string;
  fetchNotifications: (pageNum?: number) => Promise<void>;
  markRead: (notificationId: string) => Promise<void>;
  markAllRead: () => Promise<void>;
  setPage: (page: number) => void;
}

export function useNotifications(): UseNotificationsReturn {
  const { user } = useAuth();
  const [notifications, setNotifications] = React.useState<NotificationItem[]>(
    []
  );
  const [unreadCount, setUnreadCount] = React.useState(0);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [isLoading, setIsLoading] = React.useState(false);
  const [error, setError] = React.useState("");

  const fetchUnreadCount = React.useCallback(async () => {
    if (!user) return;
    try {
      const data = await apiGet<UnreadCountResponse>(
        API_ENDPOINTS.notificationsUnreadCount
      );
      setUnreadCount(data.count);
    } catch {
      // Silently fail for badge polling — don't disrupt UX
    }
  }, [user]);

  const fetchNotifications = React.useCallback(
    async (pageNum?: number) => {
      if (!user) return;
      const targetPage = pageNum ?? page;
      setIsLoading(true);
      setError("");
      try {
        const data = await apiGet<NotificationPagedResult>(
          `${API_ENDPOINTS.notifications}?page=${targetPage}&pageSize=20`
        );
        setNotifications(data.items);
        setTotalCount(data.totalCount);
        setTotalPages(data.totalPages);
        if (pageNum !== undefined) {
          setPage(pageNum);
        }
      } catch (err) {
        setError(
          err instanceof ApiError
            ? err.message
            : "Failed to load notifications."
        );
      } finally {
        setIsLoading(false);
      }
    },
    [user, page]
  );

  const markRead = React.useCallback(
    async (notificationId: string) => {
      try {
        await apiPut<MarkReadResponse>(
          `${API_ENDPOINTS.notifications}/${notificationId}/read`
        );
        setNotifications((prev) =>
          prev.map((n) =>
            n.id === notificationId ? { ...n, isRead: true } : n
          )
        );
        setUnreadCount((prev) => Math.max(0, prev - 1));
      } catch {
        // Silently fail — optimistic update already applied
      }
    },
    []
  );

  const markAllRead = React.useCallback(async (): Promise<void> => {
    try {
      await apiPut<MarkAllReadResponse>(
        API_ENDPOINTS.notificationsReadAll
      );
      setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
      setUnreadCount(0);
    } catch {
      // Silently fail
    }
  }, []);

  // Poll unread count every 60s
  React.useEffect(() => {
    if (!user) return;

    fetchUnreadCount();

    const interval = setInterval(() => {
      fetchUnreadCount();
    }, POLL_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [user, fetchUnreadCount]);

  return {
    notifications,
    unreadCount,
    totalCount,
    totalPages,
    page,
    isLoading,
    error,
    fetchNotifications,
    markRead,
    markAllRead,
    setPage,
  };
}
