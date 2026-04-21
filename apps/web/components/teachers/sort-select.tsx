"use client";

import * as React from "react";
import { ChevronDown } from "lucide-react";
import {
  teacherSortLabels,
  teacherSortValues,
  type TeacherSort,
} from "@/lib/teachers/filter-schema";
import { cn } from "@/lib/utils";

export interface SortSelectProps {
  value: TeacherSort;
  onChange: (next: TeacherSort) => void;
  /** Hide the "Recently added" option when the backend doesn't expose createdAt. */
  includeCreatedSort?: boolean;
}

export function SortSelect({
  value,
  onChange,
  includeCreatedSort = true,
}: SortSelectProps): React.ReactElement {
  const options = teacherSortValues.filter(
    (v) => includeCreatedSort || v !== "createdDesc"
  );
  const isNonDefault = value !== "nameAsc";

  return (
    <div className="relative">
      <select
        aria-label="Sort staff"
        value={value}
        onChange={(e) => onChange(e.target.value as TeacherSort)}
        className={cn(
          "focus-ring h-11 appearance-none rounded-full border bg-card/80 py-2 pl-4 pr-10 text-sm font-medium text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] transition-colors",
          isNonDefault
            ? "border-primary/40 bg-primary/5"
            : "border-border/70 hover:border-primary/30"
        )}
      >
        {options.map((key) => (
          <option key={key} value={key}>
            Sort: {teacherSortLabels[key]}
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
