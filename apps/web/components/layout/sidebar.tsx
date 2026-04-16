"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell,
  BookMarked,
  BookOpen,
  CheckCircle,
  School,
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
  School: <School className="h-5 w-5" />,
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
    <aside className="hidden md:fixed md:inset-y-4 md:left-4 md:z-40 md:flex md:w-72 md:flex-col">
      <div className="flex h-full flex-col overflow-hidden rounded-[32px] border border-white/10 bg-[linear-gradient(180deg,rgb(var(--sidebar-from)),rgb(var(--sidebar-to)))] text-white shadow-[0_35px_90px_-40px_rgba(10,14,24,0.9)]">
        <div className="border-b border-white/10 px-6 py-6">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-white/12 shadow-inner shadow-white/10">
              <div className="h-6 w-6 rounded-full bg-[linear-gradient(135deg,rgb(var(--accent)),rgb(var(--glow-2)))]" />
            </div>
            <div>
              <h1 className="text-xl font-semibold tracking-tight">{APP_NAME}</h1>
              <p className="text-sm text-white/65">School communication, refined.</p>
            </div>
          </div>
          <div className="mt-5 rounded-[24px] border border-white/10 bg-white/8 px-4 py-4 text-sm leading-6 text-white/75">
            One workspace for notices, attendance, homework, and relationships with families.
          </div>
        </div>
        <div className="px-4 pb-3 pt-5">
          <p className="px-3 text-xs font-semibold uppercase tracking-[0.28em] text-white/45">
            {user.role} workspace
          </p>
        </div>
        <nav className="flex-1 space-y-2 px-4 pb-6" role="navigation" aria-label="Main">
          {navItems.map((item) => {
            const isActive = pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "focus-ring group relative flex min-h-[56px] items-center gap-3 rounded-[22px] px-4 py-3 text-sm font-medium transition-all focus-visible:ring-offset-transparent",
                  isActive
                    ? "border-l-2 border-l-[rgb(var(--primary))] bg-white text-slate-900 shadow-[0_18px_42px_-26px_rgba(31,60,95,0.55)]"
                    : "border-l-2 border-l-transparent text-white/70 hover:bg-white/10 hover:text-white"
                )}
                aria-current={isActive ? "page" : undefined}
              >
                <span
                  className={cn(
                    "flex h-10 w-10 items-center justify-center rounded-2xl border transition-colors",
                    isActive
                      ? "border-slate-200 bg-slate-100 text-primary"
                      : "border-white/10 bg-white/6 text-white/80 group-hover:border-white/20 group-hover:bg-white/10"
                  )}
                >
                  {iconMap[item.icon] || <span className="h-5 w-5" />}
                </span>
                <span>{item.label}</span>
              </Link>
            );
          })}
        </nav>
        <div className="mx-4 mb-4 rounded-[24px] border border-white/10 bg-white/8 px-4 py-4">
          <p className="truncate text-sm font-semibold text-white">{user.name}</p>
          <p className="mt-1 text-xs uppercase tracking-[0.24em] text-white/50">{user.role}</p>
        </div>
      </div>
    </aside>
  );
}
