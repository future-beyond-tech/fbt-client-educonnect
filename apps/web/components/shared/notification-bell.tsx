"use client";

import * as React from "react";
import { Bell } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useNotifications } from "@/hooks/use-notifications";
import { NotificationPanel } from "@/components/shared/notification-panel";

export function NotificationBell(): React.ReactElement {
  const [isOpen, setIsOpen] = React.useState(false);

  const {
    notifications,
    unreadCount,
    isLoading,
    error,
    fetchNotifications,
    markRead,
    markAllRead,
  } = useNotifications();

  const handleToggle = (): void => {
    setIsOpen((prev) => !prev);
  };

  const handleClose = React.useCallback((): void => {
    setIsOpen(false);
  }, []);

  const handleLoadNotifications = React.useCallback(async (): Promise<void> => {
    await fetchNotifications(1);
  }, [fetchNotifications]);

  return (
    <div className="relative z-[60]">
      <Button
        variant="ghost"
        size="icon"
        onClick={handleToggle}
        aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ""}`}
        className="relative h-11 w-11"
      >
        <Bell className="h-5 w-5" />
        {unreadCount > 0 && (
          <span className="absolute right-1 top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold text-destructive-foreground shadow-[0_10px_18px_-10px_rgba(214,69,69,0.95)]">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </Button>

      {isOpen && (
        <>
          <div
            className="fixed inset-0 z-40 bg-slate-950/16 backdrop-blur-[2px]"
            onClick={handleClose}
            aria-hidden="true"
          />
          <NotificationPanel
            notifications={notifications}
            isLoading={isLoading}
            error={error}
            onClose={handleClose}
            onMarkRead={markRead}
            onMarkAllRead={markAllRead}
            onLoadNotifications={handleLoadNotifications}
            unreadCount={unreadCount}
          />
        </>
      )}
    </div>
  );
}
