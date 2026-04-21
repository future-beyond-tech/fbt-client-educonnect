"use client";

import * as React from "react";
import type { TeacherRoleFilter } from "@/lib/teachers/filter-schema";
import { cn } from "@/lib/utils";

export interface RolePillsProps {
  value: TeacherRoleFilter;
  onChange: (next: TeacherRoleFilter) => void;
  counts: { all: number; teacher: number; admin: number };
}

const OPTIONS: readonly { key: TeacherRoleFilter; label: string }[] = [
  { key: "all", label: "All" },
  { key: "teacher", label: "Teachers" },
  { key: "admin", label: "Admins" },
];

export function RolePills({ value, onChange, counts }: RolePillsProps): React.ReactElement {
  return (
    <div className="flex flex-wrap items-center gap-2" role="radiogroup" aria-label="Filter by role">
      {OPTIONS.map((option) => {
        const isActive = value === option.key;
        const count = counts[option.key];
        return (
          <button
            key={option.key}
            type="button"
            role="radio"
            aria-checked={isActive}
            onClick={() => onChange(option.key)}
            className={cn(
              "focus-ring inline-flex min-h-11 items-center rounded-full border px-4 py-1.5 text-xs font-medium transition-all",
              isActive
                ? "rainbow-bg border-transparent text-white shadow-[0_10px_24px_-16px_rgba(15,40,69,0.55)]"
                : "border-border/70 bg-card/70 text-muted-foreground hover:border-primary/30 hover:text-foreground"
            )}
          >
            {option.label} ({count})
          </button>
        );
      })}
    </div>
  );
}
