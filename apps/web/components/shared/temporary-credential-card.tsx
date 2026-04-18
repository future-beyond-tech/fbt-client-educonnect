"use client";

import * as React from "react";
import { Check, Copy, Eye, EyeOff, ShieldAlert } from "lucide-react";
import { cn } from "@/lib/utils";

export interface TemporaryCredentialCardProps {
  /** Human label for the credential (e.g. "Temporary Password" or "Temporary PIN"). */
  label: string;
  /** Plain-text credential returned by the API. Shown once. */
  value: string;
  /** Optional subtitle identifying the account the credential belongs to. */
  recipient?: string;
  /** Optional extra guidance for the admin (e.g. "Staff must change this on first login."). */
  helperText?: string;
  className?: string;
}

/**
 * Displays a plaintext temporary password/PIN returned from a create-user
 * response. The credential is surfaced to the admin so they can relay it to
 * the new user (who will be required to rotate it on first login). The card
 * hides the value behind a Show/Hide toggle and offers a single-click copy so
 * the admin does not have to read a long string aloud.
 *
 * IMPORTANT: This credential is shown exactly once — the API never returns it
 * again after the create response, and the hashed value is the only copy on
 * the server. Do not persist, log, or send this value through analytics.
 */
export function TemporaryCredentialCard({
  label,
  value,
  recipient,
  helperText,
  className,
}: TemporaryCredentialCardProps): React.ReactElement {
  const [revealed, setRevealed] = React.useState<boolean>(false);
  const [copied, setCopied] = React.useState<boolean>(false);

  const handleCopy = async (): Promise<void> => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API can fail in non-secure contexts; fail silently — admin
      // can still read the value off-screen via the Show toggle.
    }
  };

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        "flex flex-col gap-3 rounded-[22px] border border-amber-300/70 bg-amber-50/90 p-4 text-amber-950 shadow-[0_20px_44px_-34px_rgba(15,40,69,0.36)] backdrop-blur-sm dark:border-amber-300/20 dark:bg-amber-400/10 dark:text-amber-100",
        className
      )}
    >
      <div className="flex items-start gap-3">
        <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-white/65 dark:bg-black/10">
          <ShieldAlert
            className="h-4 w-4 text-amber-600 dark:text-amber-300"
            aria-hidden="true"
          />
        </div>
        <div className="min-w-0 space-y-1">
          <p className="text-sm font-semibold tracking-tight">{label}</p>
          {recipient ? (
            <p className="text-xs text-amber-900/90 dark:text-amber-200/90">
              Share this with {recipient}. It will not be shown again.
            </p>
          ) : (
            <p className="text-xs text-amber-900/90 dark:text-amber-200/90">
              Copy it now — it will not be shown again.
            </p>
          )}
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2 rounded-[16px] border border-amber-400/40 bg-white/70 px-3 py-2 dark:border-amber-300/25 dark:bg-black/20">
        <code
          className="flex-1 truncate font-mono text-sm tracking-wider text-foreground"
          aria-label={`${label} value`}
        >
          {revealed ? value : "•".repeat(Math.max(value.length, 4))}
        </code>
        <button
          type="button"
          onClick={(): void => setRevealed((current) => !current)}
          aria-label={revealed ? `Hide ${label.toLowerCase()}` : `Show ${label.toLowerCase()}`}
          aria-pressed={revealed}
          className="inline-flex items-center gap-1.5 rounded-full border border-amber-300/50 bg-white/80 px-3 py-1 text-xs font-semibold text-amber-900 transition hover:bg-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 dark:border-amber-300/25 dark:bg-black/30 dark:text-amber-100"
        >
          {revealed ? (
            <>
              <EyeOff className="h-3.5 w-3.5" aria-hidden="true" />
              Hide
            </>
          ) : (
            <>
              <Eye className="h-3.5 w-3.5" aria-hidden="true" />
              Show
            </>
          )}
        </button>
        <button
          type="button"
          onClick={(): void => {
            void handleCopy();
          }}
          aria-label={`Copy ${label.toLowerCase()}`}
          className="inline-flex items-center gap-1.5 rounded-full border border-amber-300/50 bg-white/80 px-3 py-1 text-xs font-semibold text-amber-900 transition hover:bg-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 dark:border-amber-300/25 dark:bg-black/30 dark:text-amber-100"
        >
          {copied ? (
            <>
              <Check className="h-3.5 w-3.5" aria-hidden="true" />
              Copied
            </>
          ) : (
            <>
              <Copy className="h-3.5 w-3.5" aria-hidden="true" />
              Copy
            </>
          )}
        </button>
      </div>

      {helperText ? (
        <p className="text-xs text-amber-900/90 dark:text-amber-200/90">
          {helperText}
        </p>
      ) : null}
    </div>
  );
}
