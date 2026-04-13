"use client";

import * as React from "react";
import { Select } from "@/components/ui/select";
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

  return (
    <Select
      id={selectId}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      error={error}
      label={label}
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
    </Select>
  );
}
