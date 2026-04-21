"use client";

import * as React from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  countActiveFilters,
  defaultTeacherFilter,
  teacherFilterFromUrlParams,
  teacherFilterToUrlParams,
  type TeacherFilter,
} from "@/lib/teachers/filter-schema";

export interface UseTeacherFiltersResult {
  filters: TeacherFilter;
  setFilter: <K extends keyof TeacherFilter>(key: K, value: TeacherFilter[K]) => void;
  clearFilter: (key: keyof TeacherFilter) => void;
  clearAll: () => void;
  activeCount: number;
}

/**
 * URL-synced filter state for the Staff page. The URL is the source of truth —
 * component state just mirrors it. Updates use `router.replace` (no history
 * entry per keystroke) with `scroll: false` so the list doesn't jump.
 */
export function useTeacherFilters(): UseTeacherFiltersResult {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Parse URL → filter on first render and on any URL change.
  const filters = React.useMemo<TeacherFilter>(() => {
    const url = new URLSearchParams(searchParams.toString());
    return teacherFilterFromUrlParams(url);
  }, [searchParams]);

  const writeFilters = React.useCallback(
    (next: TeacherFilter): void => {
      const params = teacherFilterToUrlParams(next);
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [pathname, router]
  );

  const setFilter = React.useCallback(
    <K extends keyof TeacherFilter>(key: K, value: TeacherFilter[K]): void => {
      writeFilters({ ...filters, [key]: value });
    },
    [filters, writeFilters]
  );

  const clearFilter = React.useCallback(
    (key: keyof TeacherFilter): void => {
      writeFilters({ ...filters, [key]: defaultTeacherFilter[key] });
    },
    [filters, writeFilters]
  );

  const clearAll = React.useCallback((): void => {
    writeFilters(defaultTeacherFilter);
  }, [writeFilters]);

  const activeCount = React.useMemo(() => countActiveFilters(filters), [filters]);

  return { filters, setFilter, clearFilter, clearAll, activeCount };
}
