"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell,
  BookMarked,
  BookOpen,
  CheckCircle,
  GraduationCap,
  School,
  Users,
} from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { navigationByRole } from "@/lib/constants";
import { cn } from "@/lib/utils";

const iconMap: Record<string, React.ComponentType<{ className?: string }>> = {
  Bell,
  BookMarked,
  BookOpen,
  CheckCircle,
  GraduationCap,
  School,
  Users,
};

export function BottomNav(): React.ReactElement | null {
  const { user } = useAuth();
  const pathname = usePathname();

  if (!user) return null;

  const navItems = navigationByRole[user.role];

  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-40 px-4 pb-4 md:hidden"
      role="navigation"
      aria-label="Bottom navigation"
    >
      <div className="mx-auto flex max-w-xl items-center justify-around gap-1 overflow-hidden rounded-[28px] border border-border/70 bg-[rgb(var(--page-wash)/0.92)] px-1.5 py-2 shadow-[0_22px_70px_-34px_rgba(15,40,69,0.35)] backdrop-blur-xl dark:bg-card/92 dark:shadow-[0_24px_74px_-38px_rgba(10,14,24,0.86)]">
        {navItems.map((item) => {
          const isActive = pathname.startsWith(item.href);
          const IconComponent = iconMap[item.icon];

          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "focus-ring flex min-h-[56px] min-w-0 flex-1 flex-col items-center justify-center gap-1 overflow-hidden rounded-[20px] px-1 py-2 text-[10px] font-medium uppercase tracking-[0.08em] transition-all",
                isActive
                  ? "rainbow-bg text-white shadow-[0_16px_38px_-20px_rgba(15,40,69,0.55)]"
                  : "text-muted-foreground hover:bg-card/60 hover:text-foreground"
              )}
              aria-current={isActive ? "page" : undefined}
            >
              {IconComponent && (
                <IconComponent
                  className={cn(
                    "h-5 w-5 shrink-0",
                    isActive ? "text-white" : "text-muted-foreground"
                  )}
                />
              )}
              <span className="w-full truncate text-center">{item.label}</span>
            </Link>
          );
        })}
      </div>
      <div className="h-[env(safe-area-inset-bottom)]" />
    </nav>
  );
}
