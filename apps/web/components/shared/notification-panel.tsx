"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { Bell, BookOpen, CheckCircle, Megaphone, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import type { NotificationItem } from "@/lib/types/notification";

interface NotificationPanelProps {
  notifications: NotificationItem[];
  isLoading: boolean;
  error: string;
  onClose: () => void;
  onMarkRead: (id: string) => Promise<void>;
  onMarkAllRead: () => Promise<void>;
  onLoadNotifications: () => Promise<void>;
  unreadCount: number;
}

const typeIconMap: Record<string, React.ReactNode> = {
  notice_published: (
    <Megaphone className="h-4 w-4 text-blue-500" aria-hidden="true" />
  ),
  homework_assigned: (
    <BookOpen className="h-4 w-4 text-amber-500" aria-hidden="true" />
  ),
  absence_marked: (
    <CheckCircle className="h-4 w-4 text-red-500" aria-hidden="true" />
  ),
};

function getEntityRoute(notification: NotificationItem): string | null {
  if (!notification.entityType) return null;

  switch (notification.entityType) {
    case "notice":
      return "/parent/notices";
    case "homework":
      return "/parent/homework";
    case "attendance":
      return "/parent/attendance";
    default:
      return null;
  }
}

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60_000);
  const diffHours = Math.floor(diffMs / 3_600_000);
  const diffDays = Math.floor(diffMs / 86_400_000);

  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
  });
}

function isToday(dateStr: string): boolean {
  const date = new Date(dateStr);
  const now = new Date();
  return (
    date.getDate() === now.getDate() &&
    date.getMonth() === now.getMonth() &&
    date.getFullYear() === now.getFullYear()
  );
}

export function NotificationPanel({
  notifications,
  isLoading,
  error,
  onClose,
  onMarkRead,
  onMarkAllRead,
  onLoadNotifications,
  unreadCount,
}: NotificationPanelProps): React.ReactElement {
  const router = useRouter();
  const hasLoaded = React.useRef(false);

  // Load notifications on first open
  React.useEffect(() => {
    if (!hasLoaded.current) {
      hasLoaded.current = true;
      onLoadNotifications();
    }
  }, [onLoadNotifications]);

  // Close on Escape
  React.useEffect(() => {
    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === "Escape") {
        onClose();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  const handleNotificationClick = async (
    notification: NotificationItem
  ): Promise<void> => {
    if (!notification.isRead) {
      await onMarkRead(notification.id);
    }

    const route = getEntityRoute(notification);
    if (route) {
      onClose();
      router.push(route);
    }
  };

  const todayNotifications = notifications.filter((n) => isToday(n.createdAt));
  const earlierNotifications = notifications.filter(
    (n) => !isToday(n.createdAt)
  );

  return (
    <div
      className="fixed right-4 top-[5.25rem] z-50 min-h-[200px] w-[min(calc(100vw-2rem),25rem)] origin-top-right rounded-[28px] border border-border/75 bg-[linear-gradient(180deg,rgba(255,255,255,0.98),rgba(244,248,251,0.96))] shadow-[0_34px_90px_-38px_rgba(15,23,42,0.62)] backdrop-blur-2xl dark:bg-[linear-gradient(180deg,rgba(12,30,48,0.98),rgba(8,18,31,0.96))] dark:shadow-[0_38px_96px_-42px_rgba(2,12,24,0.92)] sm:absolute sm:right-0 sm:top-full sm:mt-3"
      role="dialog"
      aria-label="Notifications"
    >
      <div className="flex items-center justify-between gap-3 border-b border-border/70 px-5 py-4">
        <div>
          <h2 className="text-sm font-semibold text-foreground">Notifications</h2>
          <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
            {unreadCount} unread
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            disabled={unreadCount === 0}
            className="h-8 rounded-full px-3 text-[11px] font-semibold uppercase tracking-[0.14em]"
            onClick={() => {
              void onMarkAllRead();
            }}
          >
            Mark all read
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 rounded-full bg-card/80"
            onClick={onClose}
            aria-label="Close notifications"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <div className="min-h-[148px] max-h-96 overflow-y-auto px-2 py-2">
        {isLoading ? (
          <div className="flex min-h-[148px] items-center justify-center py-8">
            <Spinner size="md" />
          </div>
        ) : error ? (
          <div className="flex min-h-[148px] items-center justify-center px-4 py-8 text-center text-sm text-destructive">
            {error}
          </div>
        ) : notifications.length === 0 ? (
          <div className="flex min-h-[148px] flex-col items-center justify-center py-10 text-center">
            <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-muted/40">
              <Bell
                className="h-7 w-7 text-muted-foreground"
                aria-hidden="true"
              />
            </div>
            <p className="text-sm font-medium text-foreground">
              You&apos;re all caught up!
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              No notifications yet.
            </p>
          </div>
        ) : (
          <>
            {todayNotifications.length > 0 && (
              <div>
                <p className="px-3 pt-3 pb-1 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                  Today
                </p>
                {todayNotifications.map((n) => (
                  <NotificationRow
                    key={n.id}
                    notification={n}
                    onClick={() => {
                      void handleNotificationClick(n);
                    }}
                  />
                ))}
              </div>
            )}
            {earlierNotifications.length > 0 && (
              <div>
                <p className="px-3 pt-3 pb-1 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                  Earlier
                </p>
                {earlierNotifications.map((n) => (
                  <NotificationRow
                    key={n.id}
                    notification={n}
                    onClick={() => {
                      void handleNotificationClick(n);
                    }}
                  />
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function NotificationRow({
  notification,
  onClick,
}: {
  notification: NotificationItem;
  onClick: () => void;
}): React.ReactElement {
  return (
    <button
      type="button"
      className={cn(
        "flex w-full items-start gap-3 rounded-[20px] px-3 py-3 text-left transition-all hover:bg-card/72",
        !notification.isRead && "bg-primary/8"
      )}
      onClick={onClick}
    >
      <div className="mt-0.5 shrink-0">
        {typeIconMap[notification.type] || (
          <Bell className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        )}
      </div>
      <div className="min-w-0 flex-1">
        <p
          className={cn(
            "text-sm leading-6",
            notification.isRead
              ? "text-muted-foreground"
              : "font-medium text-foreground"
          )}
        >
          {notification.title}
        </p>
        {notification.body && (
          <p className="mt-0.5 text-xs text-muted-foreground line-clamp-2">
            {notification.body}
          </p>
        )}
        <p className="mt-1 text-xs text-muted-foreground">
          {formatRelativeTime(notification.createdAt)}
        </p>
      </div>
      {!notification.isRead && (
        <div className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-primary" />
      )}
    </button>
  );
}
