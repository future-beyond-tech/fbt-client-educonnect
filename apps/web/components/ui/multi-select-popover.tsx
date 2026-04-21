"use client";

import * as React from "react";
import * as Popover from "@radix-ui/react-popover";
import { Check, ChevronDown, Search, X } from "lucide-react";
import { cn } from "@/lib/utils";

export interface MultiSelectOption {
  /** Value stored in the `value` array and returned via `onChange`. */
  value: string;
  /** Human-readable label shown in the list. */
  label: string;
}

export interface MultiSelectPopoverProps {
  /** Available options, sorted in the order they should appear. */
  options: readonly MultiSelectOption[];
  /** Currently selected option values (subset of `options[].value`). */
  value: readonly string[];
  /** Called with the new value array whenever selection changes (sorted by label). */
  onChange: (next: string[]) => void;
  /** Visible label on the trigger button when nothing is selected. */
  placeholder?: string;
  /** Placeholder shown inside the in-menu search input. */
  searchPlaceholder?: string;
  /** Custom ARIA label; defaults to `placeholder`. */
  ariaLabel?: string;
  /** Disable when the options list is still loading. */
  disabled?: boolean;
}

/**
 * Popover-based multi-select with in-menu search and keyboard navigation.
 *
 * Keyboard model (inside the open panel):
 *   - ArrowUp/ArrowDown — move focus between options
 *   - Enter / Space     — toggle the focused option
 *   - Home / End        — jump to first / last option
 *   - Escape            — close the popover (handled by Radix)
 *
 * The visible search input filters the option list; the currently focused
 * option is tracked with a roving tabindex on the options list below.
 */
export function MultiSelectPopover({
  options,
  value,
  onChange,
  placeholder = "Select",
  searchPlaceholder = "Search...",
  ariaLabel,
  disabled,
}: MultiSelectPopoverProps): React.ReactElement {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const [activeIndex, setActiveIndex] = React.useState(0);
  const listRef = React.useRef<HTMLDivElement | null>(null);

  const filtered = React.useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return options;
    return options.filter((o) => o.label.toLowerCase().includes(needle));
  }, [options, query]);

  // Keep activeIndex in range when the filter shrinks the list.
  React.useEffect(() => {
    if (activeIndex >= filtered.length) {
      setActiveIndex(filtered.length > 0 ? filtered.length - 1 : 0);
    }
  }, [filtered.length, activeIndex]);

  const toggle = React.useCallback(
    (optionValue: string): void => {
      const set = new Set(value);
      if (set.has(optionValue)) set.delete(optionValue);
      else set.add(optionValue);
      // Preserve the option order defined by the caller (sorted-by-label
      // upstream). Sorting the raw value strings would break label ordering.
      const ordered = options
        .map((o) => o.value)
        .filter((v) => set.has(v));
      onChange(ordered);
    },
    [value, options, onChange]
  );

  const clear = React.useCallback((): void => {
    onChange([]);
  }, [onChange]);

  const onListKeyDown = (event: React.KeyboardEvent<HTMLDivElement>): void => {
    if (filtered.length === 0) return;
    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        setActiveIndex((i) => (i + 1) % filtered.length);
        break;
      case "ArrowUp":
        event.preventDefault();
        setActiveIndex((i) => (i - 1 + filtered.length) % filtered.length);
        break;
      case "Home":
        event.preventDefault();
        setActiveIndex(0);
        break;
      case "End":
        event.preventDefault();
        setActiveIndex(filtered.length - 1);
        break;
      case "Enter":
      case " ":
        event.preventDefault();
        toggle(filtered[activeIndex]!.value);
        break;
      default:
        break;
    }
  };

  // When the user types in the search input, pressing ArrowDown should move
  // focus into the list. Handled by forwarding here.
  const onSearchKeyDown = (event: React.KeyboardEvent<HTMLInputElement>): void => {
    if (event.key === "ArrowDown" && filtered.length > 0) {
      event.preventDefault();
      listRef.current?.focus();
      setActiveIndex(0);
    }
  };

  const selectedCount = value.length;

  return (
    <Popover.Root open={open} onOpenChange={setOpen}>
      <Popover.Trigger asChild>
        <button
          type="button"
          disabled={disabled}
          aria-label={
            selectedCount === 0
              ? placeholder
              : `${placeholder}, ${selectedCount} selected`
          }
          className={cn(
            "focus-ring inline-flex min-h-11 items-center gap-2 rounded-full border bg-card/80 px-4 py-2 text-sm font-medium text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] transition-all",
            selectedCount > 0
              ? "border-primary/40 bg-primary/5"
              : "border-border/70 hover:border-primary/30",
            disabled && "cursor-not-allowed opacity-60"
          )}
        >
          <span>{placeholder}</span>
          {selectedCount > 0 && (
            <span
              aria-hidden="true"
              className="inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1.5 text-[11px] font-semibold text-primary-foreground"
            >
              {selectedCount}
            </span>
          )}
          <ChevronDown className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        </button>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={8}
          className="z-[90] w-[min(20rem,calc(100vw-2rem))] overflow-hidden rounded-[20px] border border-border/70 bg-card text-card-foreground shadow-[0_30px_80px_-30px_rgba(10,14,24,0.5)] [animation:enter-up_160ms_ease-out_both]"
          onOpenAutoFocus={(e) => {
            // Focus the search input, not the content root.
            e.preventDefault();
            const input = (e.currentTarget as HTMLElement).querySelector<HTMLInputElement>(
              "input[data-ms-search]"
            );
            input?.focus();
          }}
        >
          <div className="border-b border-border/60 p-2">
            <div className="relative">
              <Search
                aria-hidden="true"
                className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
              />
              <input
                data-ms-search
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={onSearchKeyDown}
                placeholder={searchPlaceholder}
                aria-label={searchPlaceholder}
                className="focus-ring h-9 w-full rounded-[14px] border border-border/60 bg-card/70 pl-9 pr-3 text-sm text-foreground placeholder:text-muted-foreground"
              />
            </div>
          </div>
          <div
            ref={listRef}
            tabIndex={-1}
            role="listbox"
            aria-label={ariaLabel ?? placeholder}
            aria-multiselectable="true"
            onKeyDown={onListKeyDown}
            className="max-h-72 overflow-y-auto p-1 focus:outline-none"
          >
            {filtered.length === 0 ? (
              <p className="px-3 py-6 text-center text-sm text-muted-foreground">
                No matches for &ldquo;{query}&rdquo;.
              </p>
            ) : (
              filtered.map((option, index) => {
                const isSelected = value.includes(option.value);
                const isActive = index === activeIndex;
                return (
                  <button
                    key={option.value}
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    tabIndex={-1}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => toggle(option.value)}
                    className={cn(
                      "flex w-full items-center gap-2 rounded-[12px] px-3 py-2 text-left text-sm transition-colors",
                      isActive
                        ? "bg-primary/10 text-foreground"
                        : "text-foreground hover:bg-muted/60"
                    )}
                  >
                    <span
                      aria-hidden="true"
                      className={cn(
                        "flex h-4 w-4 shrink-0 items-center justify-center rounded border",
                        isSelected
                          ? "border-primary bg-primary text-primary-foreground"
                          : "border-border/70 bg-card"
                      )}
                    >
                      {isSelected && <Check className="h-3 w-3" />}
                    </span>
                    <span className="truncate">{option.label}</span>
                  </button>
                );
              })
            )}
          </div>
          {selectedCount > 0 && (
            <div className="flex items-center justify-between border-t border-border/60 px-3 py-2">
              <span className="text-xs text-muted-foreground">
                {selectedCount} selected
              </span>
              <button
                type="button"
                onClick={clear}
                className="focus-ring inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-medium text-muted-foreground hover:text-foreground"
              >
                <X className="h-3 w-3" aria-hidden="true" />
                Clear
              </button>
            </div>
          )}
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
