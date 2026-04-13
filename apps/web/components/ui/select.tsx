import * as React from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";

export interface SelectProps
  extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  error?: string;
  hint?: string;
}

const Select = React.forwardRef<HTMLSelectElement, SelectProps>(
  (
    {
      className,
      label,
      error,
      hint,
      "aria-describedby": ariaDescribedBy,
      id,
      children,
      ...props
    },
    ref
  ): React.ReactElement => {
    const generatedId = React.useId();
    const selectId = id || `select-${generatedId}`;
    const errorId = error ? `${selectId}-error` : undefined;
    const hintId = hint ? `${selectId}-hint` : undefined;
    const describedBy = [ariaDescribedBy, errorId, hintId]
      .filter(Boolean)
      .join(" ");

    return (
      <div className="space-y-2">
        {label ? (
          <label
            htmlFor={selectId}
            className="block text-sm font-medium text-foreground"
          >
            {label}
          </label>
        ) : null}
        <div className="relative">
          <select
            id={selectId}
            ref={ref}
            aria-invalid={!!error}
            aria-describedby={describedBy || undefined}
            className={cn(
              "flex min-h-12 w-full appearance-none rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 pr-11 text-sm text-foreground shadow-[0_12px_30px_-26px_rgba(15,23,42,0.42)] ring-offset-background backdrop-blur-sm focus-visible:border-primary/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60",
              error && "border-destructive focus-visible:ring-destructive",
              className
            )}
            {...props}
          >
            {children}
          </select>
          <ChevronDown
            className="pointer-events-none absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
        </div>
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

Select.displayName = "Select";

export { Select };
