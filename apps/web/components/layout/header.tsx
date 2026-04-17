"use client";

import * as React from "react";
import Image from "next/image";
import { LogOut } from "lucide-react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { APP_NAME, SCHOOL_NAME } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { NotificationBell } from "@/components/shared/notification-bell";
import { ThemeToggle } from "@/components/shared/theme-toggle";

export function Header(): React.ReactElement {
  const { user, logout } = useAuth();
  const router = useRouter();
  const initials = user?.name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  const workspaceLabel =
    user?.role === "Admin"
      ? "Operations center"
      : user?.role === "Teacher"
        ? "Classroom command"
        : "Family updates";

  const handleLogout = async (): Promise<void> => {
    await logout();
    router.replace("/login");
  };

  return (
    <header className="sticky top-0 z-30 px-4 pt-4 md:pl-[19rem] md:pr-6">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-2 rounded-[28px] border border-[rgb(var(--border))] bg-[rgb(var(--page-wash)/0.85)] px-3 shadow-[0_20px_70px_-38px_rgba(15,40,69,0.35)] backdrop-blur-md dark:bg-card/84 dark:shadow-[0_26px_90px_-46px_rgba(10,14,24,0.82)] md:gap-4 md:px-5">
        <div className="flex min-w-0 flex-1 items-center gap-3">
          <Image
            src="/ris-logo.png"
            alt={SCHOOL_NAME}
            width={280}
            height={44}
            priority
            className="h-6 w-auto shrink-0 object-contain sm:h-7 md:hidden"
          />
          <div className="hidden min-w-0 md:block">
            <p className="text-xs font-semibold uppercase tracking-[0.26em] text-primary/80">
              {workspaceLabel}
            </p>
            <h1 className="truncate text-base font-semibold text-foreground md:text-lg">
              {user ? `${user.name.split(" ")[0]}'s workspace` : APP_NAME}
            </h1>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-1.5 md:gap-2">
          <ThemeToggle />
          {user && <NotificationBell />}
          {user && (
            <div className="hidden items-center gap-3 rounded-full border border-border/70 bg-[rgb(var(--muted))] px-2.5 py-2 shadow-[0_12px_30px_-24px_rgba(15,40,69,0.3)] backdrop-blur-xl dark:bg-card/90 sm:flex">
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-secondary text-xs font-semibold text-secondary-foreground">
                {initials || user.role.slice(0, 1)}
              </div>
              <div className="pr-1">
                <p className="text-sm font-semibold text-foreground">{user.name}</p>
                <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
                  {user.role}
                </p>
              </div>
            </div>
          )}
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
