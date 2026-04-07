"use client";

import * as React from "react";
import { cn } from "@/lib/utils";
import type { ClassItem } from "@/lib/types/student";

export interface ClassSelectorProps {
  classes: ClassItem[];
  value: string;
  onChange: (classId: string) => void;
  disabled?: boolean;
  label?: string;
  includeAll?: boolean;
  error?: string;
  id?: string;
}

export function ClassSelector({
  classes,
  value,
  onChange,
  disabled = false,
  label = "Class",
  includeAll = false,
  error,
  id,
}: ClassSelectorProps): React.ReactElement {
  const generatedId = React.useId();
  const selectId = id || `class-selector-${generatedId}`;
  const errorId = error ? `${selectId}-error` : undefined;

  return (
    <div className="space-y-2">
      {label && (
        <label
          htmlFor={selectId}
          className="block text-sm font-medium text-foreground"
        >
          {label}
        </label>
      )}
      <select
        id={selectId}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        aria-invalid={!!error}
        aria-describedby={errorId}
        className={cn(
          "flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm",
          error && "border-destructive focus-visible:ring-destructive"
        )}
      >
        {includeAll && <option value="">All Classes</option>}
        {!includeAll && !value && (
          <option value="" disabled>
            Select a class
          </option>
        )}
        {classes.map((cls) => (
          <option key={cls.id} value={cls.id}>
            {cls.name}
            {cls.section ? ` — ${cls.section}` : ""}
            {cls.academicYear ? ` (${cls.academicYear})` : ""}
          </option>
        ))}
      </select>
      {error && (
        <p id={errorId} className="text-sm font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
