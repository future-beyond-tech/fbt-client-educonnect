import * as React from "react";
import { cn } from "@/lib/utils";

export interface TextareaProps
  extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  error?: string;
  hint?: string;
}

const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  (
    {
      className,
      label,
      error,
      hint,
      "aria-describedby": ariaDescribedBy,
      id,
      ...props
    },
    ref
  ): React.ReactElement => {
    const generatedId = React.useId();
    const textareaId = id || `textarea-${generatedId}`;
    const errorId = error ? `${textareaId}-error` : undefined;
    const hintId = hint ? `${textareaId}-hint` : undefined;
    const describedBy = [ariaDescribedBy, errorId, hintId]
      .filter(Boolean)
      .join(" ");

    return (
      <div className="space-y-2">
        {label ? (
          <label
            htmlFor={textareaId}
            className="block text-sm font-medium text-foreground"
          >
            {label}
          </label>
        ) : null}
        <textarea
          id={textareaId}
          ref={ref}
          aria-invalid={!!error}
          aria-describedby={describedBy || undefined}
          className={cn(
            "focus-ring flex min-h-32 w-full rounded-[22px] border border-input/90 bg-card/85 px-4 py-3 text-sm text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-60",
            error && "border-destructive focus-visible:ring-destructive",
            className
          )}
          {...props}
        />
        {error ? (
          <p id={errorId} className="text-sm font-medium text-destructive">
            {error}
          </p>
        ) : null}
        {hint && !error ? (
          <p id={hintId} className="text-sm text-muted-foreground">
            {hint}
          </p>
        ) : null}
      </div>
    );
  }
);

Textarea.displayName = "Textarea";

export { Textarea };
