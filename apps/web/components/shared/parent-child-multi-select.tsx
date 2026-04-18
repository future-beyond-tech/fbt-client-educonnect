"use client";

import * as React from "react";
import { Check, Users } from "lucide-react";
import { cn } from "@/lib/utils";
import type { ParentChildItem } from "@/lib/types/student";

/**
 * Multi-child selector used by parents applying leave for one or more children
 * in a single submission.
 *
 * UX priorities (in order):
 *   1. **Obvious selection state** — big tap target, ring + check icon when
 *      selected, avoids tiny checkboxes on mobile.
 *   2. **"Select all" shortcut** — a single control flips every child on/off,
 *      including an indeterminate state when partially selected.
 *   3. **Selection summary** — the header always tells the parent what they're
 *      about to do ("Applying leave for 2 of 3 children").
 *   4. **Keyboard accessible** — each row is a real <button> so space/enter
 *      toggle selection and tab order is natural.
 *
 * Performance:
 *   - Each row is memoized so toggling one row doesn't re-render the others.
 *   - The selected set is threaded as a `Set<string>` so membership checks
 *     stay O(1) even if a parent has many linked children.
 *   - No animations that block interaction — selection feels instantaneous.
 */

interface ParentChildMultiSelectProps {
  students: ParentChildItem[];
  selectedIds: readonly string[];
  onChange: (ids: string[]) => void;
  label?: string;
  description?: string;
  disabled?: boolean;
  error?: string;
  className?: string;
}

export function ParentChildMultiSelect({
  students,
  selectedIds,
  onChange,
  label = "Select children for this leave",
  description,
  disabled = false,
  error,
  className,
}: ParentChildMultiSelectProps): React.ReactElement | null {
  const selectedSet = React.useMemo(
    () => new Set(selectedIds),
    [selectedIds]
  );

  const allSelected =
    students.length > 0 && selectedSet.size === students.length;
  const noneSelected = selectedSet.size === 0;
  const partiallySelected = !allSelected && !noneSelected;

  const headerId = React.useId();
  const descriptionId = React.useId();
  const errorId = React.useId();
  const describedBy = [
    description ? descriptionId : null,
    error ? errorId : null,
  ]
    .filter(Boolean)
    .join(" ") || undefined;

  const handleToggle = React.useCallback(
    (id: string): void => {
      if (disabled) return;
      if (selectedSet.has(id)) {
        onChange(selectedIds.filter((s) => s !== id));
      } else {
        // Preserve declaration order (matches the order the children were
        // listed), which is usually the parent's mental ordering (oldest
        // first, or by school ordering).
        const next = students
          .map((c) => c.id)
          .filter((cid) => selectedSet.has(cid) || cid === id);
        onChange(next);
      }
    },
    [students, disabled, onChange, selectedIds, selectedSet]
  );

  const handleSelectAll = React.useCallback((): void => {
    if (disabled) return;
    if (allSelected) {
      onChange([]);
    } else {
      onChange(students.map((c) => c.id));
    }
  }, [allSelected, students, disabled, onChange]);

  if (students.length === 0) {
    return null;
  }

  const summary = summarize(selectedSet.size, students.length);
  const hasMultiple = students.length > 1;

  return (
    <div
      className={cn("space-y-3", className)}
      aria-labelledby={headerId}
      aria-describedby={describedBy}
    >
      {/* Header + select-all control */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <p
            id={headerId}
            className="text-sm font-medium text-foreground"
          >
            {label}
          </p>
          <p
            id={descriptionId}
            className={cn(
              "text-xs leading-5",
              noneSelected && hasMultiple
                ? "text-amber-700 dark:text-amber-400"
                : "text-muted-foreground"
            )}
            aria-live="polite"
          >
            {description ?? summary}
          </p>
        </div>

        {hasMultiple ? (
          <button
            type="button"
            onClick={handleSelectAll}
            disabled={disabled}
            className={cn(
              "inline-flex shrink-0 items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-card",
              disabled
                ? "cursor-not-allowed border-border/60 bg-muted/30 text-muted-foreground/70"
                : allSelected
                  ? "border-primary/40 bg-primary/10 text-primary hover:bg-primary/15"
                  : partiallySelected
                    ? "border-primary/30 bg-primary/8 text-primary hover:bg-primary/12"
                    : "border-border bg-card/85 text-foreground hover:bg-muted/60"
            )}
            aria-pressed={allSelected}
          >
            <Users className="h-3.5 w-3.5" aria-hidden="true" />
            {allSelected ? "Deselect all" : "Select all children"}
          </button>
        ) : null}
      </div>

      {/* Children list */}
      <div
        role="group"
        aria-labelledby={headerId}
        className="grid gap-2 sm:grid-cols-2"
      >
        {students.map((child) => (
          <ChildRow
            key={child.id}
            child={child}
            selected={selectedSet.has(child.id)}
            disabled={disabled}
            onToggle={handleToggle}
          />
        ))}
      </div>

      {error ? (
        <p id={errorId} className="text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}

// ── Row component (memoized for perf) ─────────────────────────────────────

interface ChildRowProps {
  child: ParentChildItem;
  selected: boolean;
  disabled: boolean;
  onToggle: (id: string) => void;
}

const ChildRow = React.memo(function ChildRow({
  child,
  selected,
  disabled,
  onToggle,
}: ChildRowProps): React.ReactElement {
  const handleClick = React.useCallback((): void => {
    onToggle(child.id);
  }, [child.id, onToggle]);

  // The visible class label: "Class 5A" if we have a section, else just className.
  const classLabel = child.section
    ? `${child.className} · ${child.section}`
    : child.className;

  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={selected}
      aria-label={`${selected ? "Deselect" : "Select"} ${child.name}, ${classLabel}, roll ${child.rollNumber}`}
      onClick={handleClick}
      disabled={disabled}
      className={cn(
        "group relative flex items-center gap-3 rounded-2xl border px-4 py-3 text-left transition-all",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-card",
        disabled && "cursor-not-allowed opacity-60",
        !disabled && selected
          ? "border-primary/50 bg-primary/5 shadow-[0_10px_28px_-20px_rgba(37,99,235,0.55)] hover:bg-primary/8"
          : "border-border/70 bg-card/80 hover:border-border hover:bg-muted/50"
      )}
    >
      {/* Check indicator (big + obvious) */}
      <span
        aria-hidden="true"
        className={cn(
          "flex h-5 w-5 shrink-0 items-center justify-center rounded-md border transition-colors",
          selected
            ? "border-primary bg-primary text-primary-foreground"
            : "border-border bg-card group-hover:border-foreground/50"
        )}
      >
        {selected ? <Check className="h-3.5 w-3.5" strokeWidth={3} /> : null}
      </span>

      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold text-foreground">
          {child.name}
        </p>
        <p className="truncate text-xs text-muted-foreground">
          {classLabel} · Roll {child.rollNumber}
        </p>
      </div>
    </button>
  );
});

// ── Helpers ─────────────────────────────────────────────────────────────

function summarize(selectedCount: number, total: number): string {
  if (total <= 1) {
    return selectedCount === 1
      ? "Applying leave for your child."
      : "Select your child to apply leave.";
  }
  if (selectedCount === 0) {
    return `Select at least one child (you have ${total} linked).`;
  }
  if (selectedCount === total) {
    return `Applying leave for all ${total} children.`;
  }
  return `Applying leave for ${selectedCount} of ${total} children.`;
}
