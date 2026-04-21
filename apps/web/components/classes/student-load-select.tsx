"use client";

import * as React from "react";
import { ChevronDown } from "lucide-react";
import {
  classStudentLoadLabels,
  classStudentLoadValues,
  type ClassStudentLoad,
} from "@/lib/classes/filter-schema";
import { cn } from "@/lib/utils";

export interface ClassStudentLoadSelectProps {
  value: ClassStudentLoad | null;
  onChange: (next: ClassStudentLoad | null) => void;
}

const NONE_VALUE = "__any__";

export function ClassStudentLoadSelect({
  value,
  onChange,
}: ClassStudentLoadSelectProps): React.ReactElement {
  return (
    <div className="relative">
      <select
        aria-label="Filter by student count"
        value={value ?? NONE_VALUE}
        onChange={(e) => {
          const next = e.target.value;
          onChange(next === NONE_VALUE ? null : (next as ClassStudentLoad));
        }}
        className={cn(
          "focus-ring h-11 appearance-none rounded-full border bg-card/80 py-2 pl-4 pr-10 text-sm font-medium text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] transition-colors",
          value ? "border-primary/40 bg-primary/5" : "border-border/70 hover:border-primary/30"
        )}
      >
        <option value={NONE_VALUE}>Students: Any</option>
        {classStudentLoadValues.map((key) => (
          <option key={key} value={key}>
            Students: {classStudentLoadLabels[key]}
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
