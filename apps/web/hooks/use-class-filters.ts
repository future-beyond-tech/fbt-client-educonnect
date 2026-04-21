"use client";

import * as React from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  classFilterFromUrlParams,
  classFilterToUrlParams,
  countActiveClassFilters,
  defaultClassFilter,
  type ClassFilter,
} from "@/lib/classes/filter-schema";

export interface UseClassFiltersResult {
  filters: ClassFilter;
  setFilter: <K extends keyof ClassFilter>(key: K, value: ClassFilter[K]) => void;
  clearFilter: (key: keyof ClassFilter) => void;
  clearAll: () => void;
  activeCount: number;
}

/**
 * URL-synced state for the Classes filter bar. Mirrors
 * `useStudentFilters` / `useTeacherFilters` exactly — see those hooks for
 * the rationale behind the identity-stable memoisation.
 */
export function useClassFilters(): UseClassFiltersResult {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const filters = React.useMemo<ClassFilter>(() => {
    const url = new URLSearchParams(searchParams.toString());
    return classFilterFromUrlParams(url);
  }, [searchParams]);

  const writeFilters = React.useCallback(
    (next: ClassFilter): void => {
      const params = classFilterToUrlParams(next);
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [pathname, router]
  );

  const setFilter = React.useCallback(
    <K extends keyof ClassFilter>(key: K, value: ClassFilter[K]): void => {
      writeFilters({ ...filters, [key]: value });
    },
    [filters, writeFilters]
  );

  const clearFilter = React.useCallback(
    (key: keyof ClassFilter): void => {
      writeFilters({ ...filters, [key]: defaultClassFilter[key] });
    },
    [filters, writeFilters]
  );

  const clearAll = React.useCallback((): void => {
    writeFilters(defaultClassFilter);
  }, [writeFilters]);

  const activeCount = React.useMemo(() => countActiveClassFilters(filters), [filters]);

  return { filters, setFilter, clearFilter, clearAll, activeCount };
}
