"use client";

import * as React from "react";
import { ChevronDown } from "lucide-react";
import {
  studentSortLabels,
  studentSortValues,
  type StudentSort,
} from "@/lib/students/filter-schema";
import { cn } from "@/lib/utils";

export interface StudentSortSelectProps {
  value: StudentSort;
  onChange: (next: StudentSort) => void;
}

export function StudentSortSelect({
  value,
  onChange,
}: StudentSortSelectProps): React.ReactElement {
  const isNonDefault = value !== "default";
  return (
    <div className="relative">
      <select
        aria-label="Sort students"
        value={value}
        onChange={(e) => onChange(e.target.value as StudentSort)}
        className={cn(
          "focus-ring h-11 appearance-none rounded-full border bg-card/80 py-2 pl-4 pr-10 text-sm font-medium text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] transition-colors",
          isNonDefault
            ? "border-primary/40 bg-primary/5"
            : "border-border/70 hover:border-primary/30"
        )}
      >
        {studentSortValues.map((key) => (
          <option key={key} value={key}>
            Sort: {studentSortLabels[key]}
          </option>
        ))}
      </select>
      <ChevronDown
        aria-hidden="true"
        className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
      />
    </div>
  );
}
