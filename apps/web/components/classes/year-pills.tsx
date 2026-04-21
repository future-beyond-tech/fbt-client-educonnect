"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

export interface YearPillsProps {
  years: readonly string[];
  value: string | null;
  onChange: (next: string | null) => void;
}

/**
 * Segmented pills for the academic-year filter — one pill per year plus an
 * "All years" pill that represents the null/cleared state. Matches the
 * rainbow-gradient active style used elsewhere on the dashboard.
 */
export function YearPills({ years, value, onChange }: YearPillsProps): React.ReactElement | null {
  if (years.length <= 1) return null;

  return (
    <div className="flex flex-wrap items-center gap-2" role="radiogroup" aria-label="Filter by academic year">
      <span className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
        Academic year
      </span>
      <button
        type="button"
        role="radio"
        aria-checked={!value}
        onClick={() => onChange(null)}
        className={cn(
          "focus-ring inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-all",
          !value
            ? "rainbow-bg border-transparent text-white shadow-[0_10px_24px_-16px_rgba(15,40,69,0.55)]"
            : "border-border/70 bg-card/70 text-muted-foreground hover:border-primary/30 hover:text-foreground"
        )}
      >
        All years
      </button>
      {years.map((year) => {
        const isActive = value === year;
        return (
          <button
            key={year}
            type="button"
            role="radio"
            aria-checked={isActive}
            onClick={() => onChange(isActive ? null : year)}
            className={cn(
              "focus-ring inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-all",
              isActive
                ? "rainbow-bg border-transparent text-white shadow-[0_10px_24px_-16px_rgba(15,40,69,0.55)]"
                : "border-border/70 bg-card/70 text-muted-foreground hover:border-primary/30 hover:text-foreground"
            )}
          >
            {year}
          </button>
        );
      })}
    </div>
  );
}
