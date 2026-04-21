"use client";

import * as React from "react";
import { Search, SlidersHorizontal, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { BottomSheet } from "@/components/ui/bottom-sheet";
import { ActiveFilterChips } from "@/components/ui/active-filter-chips";
import { MultiSelectPopover } from "@/components/ui/multi-select-popover";
import { useMediaQuery } from "@/hooks/use-media-query";
import {
  buildStudentFilterChips,
  defaultStudentFilter,
  type StudentFilter,
} from "@/lib/students/filter-schema";
import { cn } from "@/lib/utils";
import { StatusSelect } from "./status-select";
import { StudentSortSelect } from "./sort-select";

export interface StudentClassOption {
  id: string;
  label: string;
}

export interface StudentFilterBarProps {
  filters: StudentFilter;
  /** Current text in the search input — may differ from filters.q during debounce. */
  searchValue: string;
  onSearchChange: (next: string) => void;
  onFilterChange: <K extends keyof StudentFilter>(key: K, value: StudentFilter[K]) => void;
  onClearAll: () => void;
  activeCount: number;
  classOptions: readonly StudentClassOption[];
  classOptionsLoading: boolean;
  searchInputRef?: React.Ref<HTMLInputElement>;
}

/**
 * Composable filter bar for /admin/students. Mirrors the teacher filter-bar
 * shape: search + dimension controls on desktop, collapsed "Filters" sheet
 * on mobile, active-chip row below.
 */
export function StudentFilterBar({
  filters,
  searchValue,
  onSearchChange,
  onFilterChange,
  onClearAll,
  activeCount,
  classOptions,
  classOptionsLoading,
  searchInputRef,
}: StudentFilterBarProps): React.ReactElement {
  const isMdUp = useMediaQuery("(min-width: 768px)");
  const [sheetOpen, setSheetOpen] = React.useState(false);

  const setClassIds = React.useCallback(
    (next: string[]): void => onFilterChange("classIds", next),
    [onFilterChange]
  );
  const setStatus = React.useCallback(
    (next: StudentFilter["status"]): void => onFilterChange("status", next),
    [onFilterChange]
  );
  const setSort = React.useCallback(
    (next: StudentFilter["sort"]): void => onFilterChange("sort", next),
    [onFilterChange]
  );

  const classLabels = React.useMemo(
    () => new Map(classOptions.map((c) => [c.id, c.label])),
    [classOptions]
  );

  const multiSelectOptions = React.useMemo(
    () => classOptions.map((c) => ({ value: c.id, label: c.label })),
    [classOptions]
  );

  const chips = buildStudentFilterChips(filters, classLabels, {
    removeSearch: () => {
      onSearchChange("");
      onFilterChange("q", "");
    },
    removeClass: (classId) =>
      setClassIds(filters.classIds.filter((id) => id !== classId)),
    removeStatus: () => onFilterChange("status", null),
    removeSort: () => onFilterChange("sort", defaultStudentFilter.sort),
  });

  const SearchInput = (
    <div className="relative w-full md:max-w-md">
      <Search
        aria-hidden="true"
        className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
      />
      <input
        ref={searchInputRef}
        id="student-search"
        type="search"
        value={searchValue}
        onChange={(e) => onSearchChange(e.target.value)}
        placeholder="Search by name or roll number..."
        aria-label="Search students"
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
    <div className="flex flex-wrap items-center gap-2">
      <MultiSelectPopover
        options={multiSelectOptions}
        value={filters.classIds}
        onChange={setClassIds}
        placeholder="Classes"
        searchPlaceholder="Search classes..."
        disabled={classOptionsLoading && classOptions.length === 0}
      />
      <StatusSelect value={filters.status} onChange={setStatus} />
      <StudentSortSelect value={filters.sort} onChange={setSort} />
    </div>
  );

  return (
    <div className="space-y-4">
      <div className="relative overflow-hidden rounded-[24px] border border-border/70 bg-card/80 p-4 shadow-[0_18px_46px_-34px_rgba(15,40,69,0.3)] dark:bg-card/90 md:p-5">
        <div
          aria-hidden="true"
          className="rainbow-bg pointer-events-none absolute inset-x-0 top-0 h-0.5"
        />
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          {SearchInput}
          {isMdUp ? (
            DimensionControls
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
        chips={chips}
        onClearAll={() => {
          onSearchChange("");
          onClearAll();
        }}
      />

      {!isMdUp && (
        <BottomSheet
          open={sheetOpen}
          onOpenChange={setSheetOpen}
          title="Filter students"
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
