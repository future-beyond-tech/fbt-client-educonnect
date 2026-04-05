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
  const panelRef = React.useRef<HTMLDivElement>(null);
  const hasLoaded = React.useRef(false);

  // Load notifications on first open
  React.useEffect(() => {
    if (!hasLoaded.current) {
      hasLoaded.current = true;
      onLoadNotifications();
    }
  }, [onLoadNotifications]);

  // Close on click outside
  React.useEffect(() => {
    function handleClickOutside(event: MouseEvent): void {
      if (
        panelRef.current &&
        !panelRef.current.contains(event.target as Node)
      ) {
        onClose();
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [onClose]);

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
      ref={panelRef}
      className="absolute right-0 top-full mt-2 z-50 w-80 sm:w-96 rounded-lg border border-border bg-card shadow-lg"
      role="dialog"
      aria-label="Notifications"
    >
      {/* Header */}
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h2 className="text-sm font-semibold text-foreground">Notifications</h2>
        <div className="flex items-center gap-2">
          {unreadCount > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={() => {
                void onMarkAllRead();
              }}
            >
              Mark all read
            </Button>
          )}
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7"
            onClick={onClose}
            aria-label="Close notifications"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Body */}
      <div className="max-h-96 overflow-y-auto">
        {isLoading ? (
          <div className="flex items-center justify-center py-8">
            <Spinner size="md" />
          </div>
        ) : error ? (
          <div className="px-4 py-8 text-center text-sm text-destructive">
            {error}
          </div>
        ) : notifications.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-8 text-center">
            <Bell
              className="mb-2 h-8 w-8 text-muted-foreground"
              aria-hidden="true"
            />
            <p className="text-sm text-muted-foreground">
              No notifications yet.
            </p>
          </div>
        ) : (
          <>
            {todayNotifications.length > 0 && (
              <div>
                <p className="px-4 pt-3 pb-1 text-xs font-medium uppercase text-muted-foreground">
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
                <p className="px-4 pt-3 pb-1 text-xs font-medium uppercase text-muted-foreground">
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
        "flex w-full items-start gap-3 px-4 py-3 text-left transition-colors hover:bg-accent",
        !notification.isRead && "bg-primary/5"
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
            "text-sm leading-tight",
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
