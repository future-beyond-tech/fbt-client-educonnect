"use client";

import * as React from "react";
import { Search, SlidersHorizontal, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { BottomSheet } from "@/components/ui/bottom-sheet";
import { useMediaQuery } from "@/hooks/use-media-query";
import {
  buildTeacherFilterChips,
  defaultTeacherFilter,
  type TeacherFilter,
  type TeacherRoleFilter,
} from "@/lib/teachers/filter-schema";
import { cn } from "@/lib/utils";
import { ActiveFilterChips } from "@/components/ui/active-filter-chips";
import { MultiSelectPopover } from "@/components/ui/multi-select-popover";
import { ClassLoadSelect } from "./class-load-select";
import { RolePills } from "./role-pills";
import { SortSelect } from "./sort-select";

export interface TeacherFilterBarProps {
  filters: TeacherFilter;
  /** Current text in the search input (may differ from `filters.q` during debounce). */
  searchValue: string;
  onSearchChange: (next: string) => void;
  onFilterChange: <K extends keyof TeacherFilter>(key: K, value: TeacherFilter[K]) => void;
  onClearAll: () => void;
  activeCount: number;
  roleCounts: { all: number; teacher: number; admin: number };
  subjectOptions: readonly string[];
  subjectOptionsLoading: boolean;
  includeCreatedSort: boolean;
  /**
   * Ref to the search input so the page can wire up the `/` shortcut to focus it.
   */
  searchInputRef?: React.Ref<HTMLInputElement>;
}

/**
 * Composable filter bar for the Staff page. Desktop: inline search + role
 * pills + subject/class-load/sort selects. Mobile (< md): collapses to a
 * single search box and a "Filters" button that opens a BottomSheet
 * containing every dimension.
 *
 * Active-filter chips render below the card on both viewports.
 */
export function TeacherFilterBar({
  filters,
  searchValue,
  onSearchChange,
  onFilterChange,
  onClearAll,
  activeCount,
  roleCounts,
  subjectOptions,
  subjectOptionsLoading,
  includeCreatedSort,
  searchInputRef,
}: TeacherFilterBarProps): React.ReactElement {
  const isMdUp = useMediaQuery("(min-width: 768px)");
  const [sheetOpen, setSheetOpen] = React.useState(false);

  const setRole = React.useCallback(
    (next: TeacherRoleFilter): void => onFilterChange("role", next),
    [onFilterChange]
  );
  const setSubjects = React.useCallback(
    (next: string[]): void => onFilterChange("subjects", next),
    [onFilterChange]
  );
  const setLoad = React.useCallback(
    (next: TeacherFilter["load"]): void => onFilterChange("load", next),
    [onFilterChange]
  );
  const setSort = React.useCallback(
    (next: TeacherFilter["sort"]): void => onFilterChange("sort", next),
    [onFilterChange]
  );

  const SearchInput = (
    <div className="relative w-full md:max-w-md">
      <Search
        aria-hidden="true"
        className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
      />
      <input
        ref={searchInputRef}
        id="staff-search"
        type="search"
        value={searchValue}
        onChange={(e) => onSearchChange(e.target.value)}
        placeholder="Search by name or phone..."
        aria-label="Search staff"
        className="focus-ring h-11 w-full rounded-[18px] border border-border/70 bg-card/80 pl-10 pr-10 text-sm text-foreground placeholder:text-muted-foreground shadow-[0_12px_36px_-30px_rgba(15,40,69,0.4)]"
      />
      {searchValue && (
        <button
          type="button"
          onClick={() => onSearchChange("")}
          aria-label="Clear search"
          className="focus-ring absolute right-2 top-1/2 inline-flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-full text-muted-foreground hover:bg-muted/60 hover:text-foreground"
        >
          <X className="h-4 w-4" aria-hidden="true" />
        </button>
      )}
    </div>
  );

  const DimensionControls = (
    <>
      <RolePills value={filters.role} onChange={setRole} counts={roleCounts} />
      <div className="flex flex-wrap items-center gap-2">
        <MultiSelectPopover
          options={subjectOptions.map((s) => ({ value: s, label: s }))}
          value={filters.subjects}
          onChange={setSubjects}
          placeholder="Subjects"
          searchPlaceholder="Search subjects..."
          disabled={subjectOptionsLoading && subjectOptions.length === 0}
        />
        <ClassLoadSelect value={filters.load} onChange={setLoad} />
        <SortSelect
          value={filters.sort}
          onChange={setSort}
          includeCreatedSort={includeCreatedSort}
        />
      </div>
    </>
  );

  return (
    <div className="space-y-4">
      {/* Card shell — rainbow accent strip + translucent surface to match existing pages. */}
      <div className="relative overflow-hidden rounded-[24px] border border-border/70 bg-card/80 p-4 shadow-[0_18px_46px_-34px_rgba(15,40,69,0.3)] dark:bg-card/90 md:p-5">
        <div
          aria-hidden="true"
          className="rainbow-bg pointer-events-none absolute inset-x-0 top-0 h-0.5"
        />
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          {SearchInput}
          {isMdUp ? (
            <div className="flex flex-wrap items-center gap-3">{DimensionControls}</div>
          ) : (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setSheetOpen(true)}
              className="self-start"
              aria-expanded={sheetOpen}
            >
              <SlidersHorizontal className="h-4 w-4" aria-hidden="true" />
              Filters
              {activeCount > 0 && (
                <span
                  aria-label={`${activeCount} active`}
                  className="ml-1 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1.5 text-[11px] font-semibold text-primary-foreground"
                >
                  {activeCount}
                </span>
              )}
            </Button>
          )}
        </div>
      </div>

      <ActiveFilterChips
        chips={buildTeacherFilterChips(filters, {
          removeSearch: () => {
            onSearchChange("");
            onFilterChange("q", "");
          },
          removeRole: () => onFilterChange("role", defaultTeacherFilter.role),
          removeSubject: (subject) =>
            setSubjects(filters.subjects.filter((s) => s !== subject)),
          removeLoad: () => onFilterChange("load", null),
          removeSort: () => onFilterChange("sort", defaultTeacherFilter.sort),
        })}
        onClearAll={() => {
          onSearchChange("");
          onClearAll();
        }}
      />

      {!isMdUp && (
        <BottomSheet
          open={sheetOpen}
          onOpenChange={setSheetOpen}
          title="Filter staff"
          description="Combine any filters below. Updates apply instantly."
          footer={
            <>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  onSearchChange("");
                  onClearAll();
                }}
                disabled={activeCount === 0}
              >
                Clear all
              </Button>
              <Button type="button" onClick={() => setSheetOpen(false)}>
                Done
              </Button>
            </>
          }
        >
          <div className={cn("space-y-5")}>{DimensionControls}</div>
        </BottomSheet>
      )}
    </div>
  );
}
