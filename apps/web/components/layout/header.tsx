"use client";

import * as React from "react";
import { LogOut } from "lucide-react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { APP_NAME } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { NotificationBell } from "@/components/shared/notification-bell";

export function Header(): React.ReactElement {
  const { user, logout } = useAuth();
  const router = useRouter();

  const handleLogout = async (): Promise<void> => {
    await logout();
    router.replace("/login");
  };

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-card">
      <div className="flex h-14 items-center justify-between gap-4 px-4 md:ml-64 md:h-16">
        {/* Mobile: show app name; Desktop: hidden (sidebar has it) */}
        <h1 className="text-lg font-bold text-primary md:hidden">
          {APP_NAME}
        </h1>

        {/* Desktop: spacer */}
        <div className="hidden md:block" />

        <div className="flex items-center gap-3">
          {user && (
            <div className="hidden sm:flex flex-col items-end">
              <p className="text-sm font-medium text-foreground">{user.name}</p>
              <p className="text-xs text-muted-foreground capitalize">
                {user.role}
              </p>
            </div>
          )}
          {user && <NotificationBell />}
          {user && (
            <Button
              variant="ghost"
              size="icon"
              onClick={() => {
                void handleLogout();
              }}
              aria-label="Logout"
              className="h-11 w-11"
            >
              <LogOut className="h-5 w-5" />
            </Button>
          )}
        </div>
      </div>
    </header>
  );
}
