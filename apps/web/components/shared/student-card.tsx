"use client";

import * as React from "react";
import { Badge } from "@/components/ui/badge";

export interface StudentCardProps {
  name: string;
  rollNumber: string;
  className: string;
  section: string;
  isActive: boolean;
  showInactiveBadge?: boolean;
  onClick?: () => void;
}

export function StudentCard({
  name,
  rollNumber,
  className,
  section,
  isActive,
  showInactiveBadge = true,
  onClick,
}: StudentCardProps): React.ReactElement {
  const Wrapper = onClick ? "button" : "div";
  const initials = name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");

  return (
    <Wrapper
      onClick={onClick}
      className={`flex w-full items-center justify-between gap-4 rounded-[26px] border border-border/70 bg-card/86 p-4 text-left shadow-[0_22px_60px_-42px_rgba(15,23,42,0.45)] transition-all backdrop-blur-sm dark:bg-card/92 dark:shadow-[0_24px_68px_-44px_rgba(2,12,24,0.84)] ${
        onClick
          ? "cursor-pointer hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          : ""
      } ${!isActive ? "opacity-60" : ""}`}
      aria-label={`Student ${name}, roll number ${rollNumber}`}
    >
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[18px] bg-[linear-gradient(135deg,rgba(22,88,136,0.14),rgba(58,199,179,0.14))] text-sm font-semibold text-primary">
          {initials || "ST"}
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate text-base font-semibold text-foreground">{name}</p>
          <p className="text-sm text-muted-foreground">Roll: {rollNumber}</p>
        </div>
      </div>
      <div className="ml-3 flex shrink-0 flex-wrap items-center justify-end gap-2">
        <Badge variant="secondary">
          {className}
          {section ? ` ${section}` : ""}
        </Badge>
        {showInactiveBadge && !isActive && (
          <Badge variant="destructive">Inactive</Badge>
        )}
      </div>
    </Wrapper>
  );
}
