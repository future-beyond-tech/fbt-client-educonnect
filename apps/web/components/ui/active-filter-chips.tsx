"use client";

import * as React from "react";
import { AnimatePresence, motion } from "framer-motion";
import { X } from "lucide-react";

export interface FilterChip {
  /** Stable key for AnimatePresence. */
  key: string;
  /** Display label inside the chip (e.g. "Subject: Mathematics"). */
  label: string;
  /** Invoked when the user clicks the × on this chip. */
  onRemove: () => void;
}

export interface ActiveFilterChipsProps {
  chips: readonly FilterChip[];
  onClearAll: () => void;
}

/**
 * Presentational chip row. Building the list of chips from a page-specific
 * filter object is the caller's job — this component just renders what it's
 * given and animates enter/exit.
 */
export function ActiveFilterChips({
  chips,
  onClearAll,
}: ActiveFilterChipsProps): React.ReactElement | null {
  if (chips.length === 0) return null;

  return (
    <div
      className="flex flex-wrap items-center gap-2"
      role="region"
      aria-label="Active filters"
    >
      <AnimatePresence initial={false} mode="popLayout">
        {chips.map((chip) => (
          <motion.span
            key={chip.key}
            layout
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.9 }}
            transition={{ duration: 0.15 }}
            className="inline-flex items-center gap-1.5 rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-medium text-foreground"
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
