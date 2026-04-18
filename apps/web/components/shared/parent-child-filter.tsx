"use client";

import * as React from "react";
import { Select } from "@/components/ui/select";
import { ALL_CHILDREN_VALUE } from "@/lib/parent-children";
import type { ParentChildItem } from "@/lib/types/student";

interface ParentChildFilterProps {
  students: ParentChildItem[];
  value: string;
  onChange: (value: string) => void;
  label?: string;
  includeAllOption?: boolean;
  disabled?: boolean;
  className?: string;
}

export function ParentChildFilter({
  students,
  value,
  onChange,
  label = "Child",
  includeAllOption = true,
  disabled = false,
  className,
}: ParentChildFilterProps): React.ReactElement | null {
  if (students.length === 0) {
    return null;
  }

  const showAllOption = includeAllOption && students.length > 1;

  return (
    <Select
      label={label}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      disabled={disabled}
      className={className}
    >
      {showAllOption ? (
        <option value={ALL_CHILDREN_VALUE}>All children</option>
      ) : null}
      {students.map((child) => (
        <option key={child.id} value={child.id}>
          {child.name} ({child.rollNumber})
        </option>
      ))}
    </Select>
  );
}
