"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell,
  BookMarked,
  BookOpen,
  CheckCircle,
  Users,
} from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { navigationByRole, APP_NAME } from "@/lib/constants";
import { cn } from "@/lib/utils";

const iconMap: Record<string, React.ReactNode> = {
  Bell: <Bell className="h-5 w-5" />,
  BookMarked: <BookMarked className="h-5 w-5" />,
  BookOpen: <BookOpen className="h-5 w-5" />,
  CheckCircle: <CheckCircle className="h-5 w-5" />,
  Users: <Users className="h-5 w-5" />,
};

export function Sidebar(): React.ReactElement {
  const { user } = useAuth();
  const pathname = usePathname();

  if (!user) {
    return <aside className="hidden md:fixed md:inset-y-0 md:w-64" aria-hidden="true" />;
  }

  const navItems = navigationByRole[user.role];

  return (
    <aside className="hidden border-r border-border bg-card md:fixed md:inset-y-0 md:z-50 md:flex md:w-64 md:flex-col">
      <div className="flex h-16 items-center border-b border-border px-6">
        <h1 className="text-xl font-bold text-primary">{APP_NAME}</h1>
      </div>
      <nav className="flex-1 space-y-1 px-2 py-4" role="navigation" aria-label="Main">
        {navItems.map((item) => {
          const isActive = pathname.startsWith(item.href);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring min-h-[44px]",
                isActive
                  ? "bg-primary text-primary-foreground"
                  : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              )}
              aria-current={isActive ? "page" : undefined}
            >
              {iconMap[item.icon] || <span className="h-5 w-5" />}
              <span>{item.label}</span>
            </Link>
          );
        })}
      </nav>
      {user && (
        <div className="border-t border-border px-4 py-3">
          <p className="text-sm font-medium text-foreground truncate">{user.name}</p>
          <p className="text-xs text-muted-foreground capitalize">{user.role}</p>
        </div>
      )}
    </aside>
  );
}
