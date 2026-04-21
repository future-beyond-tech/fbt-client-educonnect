"use client";

import * as React from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  countActiveStudentFilters,
  defaultStudentFilter,
  studentFilterFromUrlParams,
  studentFilterToUrlParams,
  type StudentFilter,
} from "@/lib/students/filter-schema";

export interface UseStudentFiltersResult {
  filters: StudentFilter;
  setFilter: <K extends keyof StudentFilter>(key: K, value: StudentFilter[K]) => void;
  clearFilter: (key: keyof StudentFilter) => void;
  clearAll: () => void;
  activeCount: number;
}

/**
 * URL-synced state for the admin Students filter bar. URL is the source of
 * truth — component state mirrors it. Writes use `router.replace` with
 * `scroll: false` so the list doesn't jump on each keystroke.
 */
export function useStudentFilters(): UseStudentFiltersResult {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const filters = React.useMemo<StudentFilter>(() => {
    const url = new URLSearchParams(searchParams.toString());
    return studentFilterFromUrlParams(url);
  }, [searchParams]);

  const writeFilters = React.useCallback(
    (next: StudentFilter): void => {
      const params = studentFilterToUrlParams(next);
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [pathname, router]
  );

  const setFilter = React.useCallback(
    <K extends keyof StudentFilter>(key: K, value: StudentFilter[K]): void => {
      writeFilters({ ...filters, [key]: value });
    },
    [filters, writeFilters]
  );

  const clearFilter = React.useCallback(
    (key: keyof StudentFilter): void => {
      writeFilters({ ...filters, [key]: defaultStudentFilter[key] });
    },
    [filters, writeFilters]
  );

  const clearAll = React.useCallback((): void => {
    writeFilters(defaultStudentFilter);
  }, [writeFilters]);

  const activeCount = React.useMemo(() => countActiveStudentFilters(filters), [filters]);

  return { filters, setFilter, clearFilter, clearAll, activeCount };
}
