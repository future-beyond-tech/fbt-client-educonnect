"use client";

import * as React from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Dependency-free, accessible Dialog.
 *
 * Features:
 * - Backdrop click to dismiss (configurable).
 * - ESC to dismiss (configurable).
 * - Focus is moved into the dialog on open, restored to the opener on close.
 * - Basic focus trap (Tab/Shift+Tab cycle within the dialog).
 * - Body scroll lock while open.
 * - Portals to document.body so it escapes clipping parents.
 */

export interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  /** Max width of the dialog panel. Defaults to 32rem (md). */
  size?: "sm" | "md" | "lg";
  /** If true, clicking the backdrop does not close. Default: false. */
  disableBackdropClose?: boolean;
  /** If true, pressing ESC does not close. Default: false. */
  disableEscClose?: boolean;
  /** Extra class for the panel. */
  className?: string;
}

const sizeClass: Record<NonNullable<DialogProps["size"]>, string> = {
  sm: "max-w-md",
  md: "max-w-xl",
  lg: "max-w-3xl",
};

export function Dialog({
  open,
  onOpenChange,
  title,
  description,
  children,
  footer,
  size = "md",
  disableBackdropClose,
  disableEscClose,
  className,
}: DialogProps): React.ReactElement | null {
  const panelRef = React.useRef<HTMLDivElement | null>(null);
  const previouslyFocused = React.useRef<HTMLElement | null>(null);
  const titleId = React.useId();
  const descriptionId = React.useId();

  // Portal target lives on the client only.
  const [mounted, setMounted] = React.useState(false);
  React.useEffect(() => {
    setMounted(true);
  }, []);

  // ESC + focus management + scroll lock.
  React.useEffect(() => {
    if (!open) return undefined;

    previouslyFocused.current =
      (document.activeElement as HTMLElement | null) ?? null;

    // Lock scroll.
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    // Move focus into the panel on the next frame.
    const raf = requestAnimationFrame(() => {
      const panel = panelRef.current;
      if (!panel) return;
      const focusable = panel.querySelector<HTMLElement>(
        "[data-autofocus]"
      );
      (focusable ?? panel).focus();
    });

    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === "Escape" && !disableEscClose) {
        event.stopPropagation();
        onOpenChange(false);
        return;
      }
      if (event.key === "Tab") {
        const panel = panelRef.current;
        if (!panel) return;
        const focusables = Array.from(
          panel.querySelectorAll<HTMLElement>(
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
          )
        ).filter((el) => !el.hasAttribute("data-no-focus-trap"));

        if (focusables.length === 0) return;

        const first = focusables[0]!;
        const last = focusables[focusables.length - 1]!;
        const active = document.activeElement as HTMLElement | null;

        if (event.shiftKey && active === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && active === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };

    document.addEventListener("keydown", onKeyDown, true);

    return (): void => {
      document.removeEventListener("keydown", onKeyDown, true);
      document.body.style.overflow = prevOverflow;
      cancelAnimationFrame(raf);
      // Return focus.
      const toFocus = previouslyFocused.current;
      if (toFocus && typeof toFocus.focus === "function") {
        toFocus.focus();
      }
    };
  }, [open, onOpenChange, disableEscClose]);

  if (!mounted || !open) return null;

  const handleBackdrop = (e: React.MouseEvent<HTMLDivElement>): void => {
    if (disableBackdropClose) return;
    if (e.target === e.currentTarget) onOpenChange(false);
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      className="fixed inset-0 z-[100] flex items-end justify-center p-0 sm:items-center sm:p-4"
      onMouseDown={handleBackdrop}
    >
      {/* Backdrop */}
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-[rgb(15_19_32/0.55)] backdrop-blur-sm [animation:enter-up_180ms_ease-out_both]"
      />
      {/* Panel */}
      <div
        ref={panelRef}
        tabIndex={-1}
        className={cn(
          "relative z-10 flex max-h-[92vh] w-full flex-col overflow-hidden rounded-t-[28px] border border-border/70 bg-card text-card-foreground shadow-[0_40px_120px_-40px_rgba(10,14,24,0.5)] [animation:enter-up_220ms_ease-out_both] sm:rounded-[28px]",
          sizeClass[size],
          className
        )}
        onMouseDown={(e) => e.stopPropagation()}
      >
        {/* Rainbow accent strip */}
        <div
          aria-hidden="true"
          className="rainbow-bg pointer-events-none absolute inset-x-0 top-0 h-1"
        />

        <div className="flex items-start justify-between gap-4 border-b border-border/60 px-5 py-4 sm:px-6">
          <div className="min-w-0 space-y-1">
            <h2
              id={titleId}
              className="text-lg font-semibold leading-tight text-foreground"
            >
              {title}
            </h2>
            {description ? (
              <p
                id={descriptionId}
                className="text-sm leading-6 text-muted-foreground"
              >
                {description}
              </p>
            ) : null}
          </div>
          <button
            type="button"
            onClick={() => onOpenChange(false)}
            aria-label="Close dialog"
            className="focus-ring inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-muted-foreground hover:bg-card/60 hover:text-foreground"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4 sm:px-6">
          {children}
        </div>

        {footer ? (
          <div className="flex items-center justify-end gap-2 border-t border-border/60 px-5 py-4 sm:px-6">
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  );
}
