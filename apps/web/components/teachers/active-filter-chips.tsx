"use client";

import * as React from "react";
import { AnimatePresence, motion } from "framer-motion";
import { X } from "lucide-react";
import {
  teacherClassLoadLabels,
  teacherSortLabels,
  type TeacherFilter,
} from "@/lib/teachers/filter-schema";
import { cn } from "@/lib/utils";

export interface ActiveFilterChipsProps {
  filters: TeacherFilter;
  onRemoveSearch: () => void;
  onRemoveRole: () => void;
  onRemoveSubject: (subject: string) => void;
  onRemoveLoad: () => void;
  onRemoveSort: () => void;
  onClearAll: () => void;
}

interface Chip {
  key: string;
  label: string;
  onRemove: () => void;
}

/**
 * Builds the list of active filter chips from the current filter. Returns
 * a stable-keyed array so Framer Motion can animate chip enter/exit.
 */
function buildChips(
  filters: TeacherFilter,
  handlers: Omit<ActiveFilterChipsProps, "filters" | "onClearAll">
): Chip[] {
  const chips: Chip[] = [];
  if (filters.q.trim()) {
    chips.push({
      key: "search",
      label: `Search: ${filters.q.trim()}`,
      onRemove: handlers.onRemoveSearch,
    });
  }
  if (filters.role !== "all") {
    chips.push({
      key: "role",
      label: `Role: ${filters.role === "teacher" ? "Teachers" : "Admins"}`,
      onRemove: handlers.onRemoveRole,
    });
  }
  for (const subject of filters.subjects) {
    chips.push({
      key: `subject:${subject}`,
      label: `Subject: ${subject}`,
      onRemove: () => handlers.onRemoveSubject(subject),
    });
  }
  if (filters.load) {
    chips.push({
      key: "load",
      label: `Class-load: ${teacherClassLoadLabels[filters.load]}`,
      onRemove: handlers.onRemoveLoad,
    });
  }
  if (filters.sort !== "nameAsc") {
    chips.push({
      key: "sort",
      label: `Sort: ${teacherSortLabels[filters.sort]}`,
      onRemove: handlers.onRemoveSort,
    });
  }
  return chips;
}

export function ActiveFilterChips({
  filters,
  onClearAll,
  ...handlers
}: ActiveFilterChipsProps): React.ReactElement | null {
  const chips = buildChips(filters, handlers);
  if (chips.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-2" aria-label="Active filters">
      <AnimatePresence initial={false} mode="popLayout">
        {chips.map((chip) => (
          <motion.span
            key={chip.key}
            layout
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.9 }}
            transition={{ duration: 0.15 }}
            className={cn(
              "inline-flex items-center gap-1.5 rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-medium text-foreground"
            )}
          >
            <span>{chip.label}</span>
            <button
              type="button"
              onClick={chip.onRemove}
              aria-label={`Remove filter: ${chip.label}`}
              className="focus-ring inline-flex h-4 w-4 items-center justify-center rounded-full text-muted-foreground hover:bg-primary/20 hover:text-foreground"
            >
              <X className="h-3 w-3" aria-hidden="true" />
            </button>
          </motion.span>
        ))}
      </AnimatePresence>
      <button
        type="button"
        onClick={onClearAll}
        className="focus-ring ml-auto inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-medium text-muted-foreground hover:text-foreground"
      >
        Clear all
      </button>
    </div>
  );
}
