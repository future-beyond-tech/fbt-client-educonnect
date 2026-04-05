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
    <div className="relative">
      <Button
        variant="ghost"
        size="icon"
        onClick={handleToggle}
        aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ""}`}
        className="h-11 w-11"
      >
        <Bell className="h-5 w-5" />
        {unreadCount > 0 && (
          <span className="absolute right-1.5 top-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold text-destructive-foreground">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </Button>

      {isOpen && (
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
      )}
    </div>
  );
}
