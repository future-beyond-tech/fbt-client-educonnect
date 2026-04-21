"use client";

import * as React from "react";
import { Search, SlidersHorizontal, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { BottomSheet } from "@/components/ui/bottom-sheet";
import { ActiveFilterChips } from "@/components/ui/active-filter-chips";
import { useMediaQuery } from "@/hooks/use-media-query";
import {
  buildClassFilterChips,
  defaultClassFilter,
  type ClassFilter,
} from "@/lib/classes/filter-schema";
import { cn } from "@/lib/utils";
import { ClassSortSelect } from "./class-sort-select";
import { ClassStudentLoadSelect } from "./student-load-select";
import { YearPills } from "./year-pills";

export interface ClassFilterBarProps {
  filters: ClassFilter;
  onFilterChange: <K extends keyof ClassFilter>(key: K, value: ClassFilter[K]) => void;
  onClearAll: () => void;
  activeCount: number;
  availableYears: readonly string[];
  searchInputRef?: React.Ref<HTMLInputElement>;
}

/**
 * Composable filter bar for /admin/classes. Filtering is entirely
 * client-side — see applyClassFilter in the schema — so the bar owns only
 * the search text, the year pill selection, student-load, and sort.
 */
export function ClassFilterBar({
  filters,
  onFilterChange,
  onClearAll,
  activeCount,
  availableYears,
  searchInputRef,
}: ClassFilterBarProps): React.ReactElement {
  const isMdUp = useMediaQuery("(min-width: 768px)");
  const [sheetOpen, setSheetOpen] = React.useState(false);

  const setYear = React.useCallback(
    (next: ClassFilter["year"]): void => onFilterChange("year", next),
    [onFilterChange]
  );
  const setLoad = React.useCallback(
    (next: ClassFilter["studentLoad"]): void => onFilterChange("studentLoad", next),
    [onFilterChange]
  );
  const setSort = React.useCallback(
    (next: ClassFilter["sort"]): void => onFilterChange("sort", next),
    [onFilterChange]
  );
  const setSearch = React.useCallback(
    (next: string): void => onFilterChange("q", next),
    [onFilterChange]
  );

  const chips = buildClassFilterChips(filters, {
    removeSearch: () => setSearch(""),
    removeYear: () => setYear(null),
    removeStudentLoad: () => setLoad(null),
    removeSort: () => setSort(defaultClassFilter.sort),
  });

  const SearchInput = (
    <div className="relative w-full md:max-w-md">
      <Search
        aria-hidden="true"
        className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
      />
      <input
        ref={searchInputRef}
        id="class-search"
        type="search"
        value={filters.q}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by class, section, or year..."
        aria-label="Search classes"
        className="focus-ring h-11 w-full rounded-[18px] border border-border/70 bg-card/80 pl-10 pr-10 text-sm text-foreground placeholder:text-muted-foreground shadow-[0_12px_36px_-30px_rgba(15,40,69,0.4)]"
      />
      {filters.q && (
        <button
          type="button"
          onClick={() => setSearch("")}
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
      <ClassStudentLoadSelect value={filters.studentLoad} onChange={setLoad} />
      <ClassSortSelect value={filters.sort} onChange={setSort} />
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
        {isMdUp && (
          <div className="mt-3">
            <YearPills years={availableYears} value={filters.year} onChange={setYear} />
          </div>
        )}
      </div>

      <ActiveFilterChips chips={chips} onClearAll={onClearAll} />

      {!isMdUp && (
        <BottomSheet
          open={sheetOpen}
          onOpenChange={setSheetOpen}
          title="Filter classes"
          description="Combine any filters below. Updates apply instantly."
          footer={
            <>
              <Button
                type="button"
                variant="outline"
                onClick={onClearAll}
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
          <div className={cn("space-y-5")}>
            <YearPills years={availableYears} value={filters.year} onChange={setYear} />
            {DimensionControls}
          </div>
        </BottomSheet>
      )}
    </div>
  );
}
