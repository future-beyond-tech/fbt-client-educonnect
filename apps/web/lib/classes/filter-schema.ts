import { z } from "zod";
import type { FilterChip } from "@/components/ui/active-filter-chips";
import type { ClassItem } from "@/lib/types/student";

/**
 * Single source of truth for the admin Classes filter bar.
 *
 * Filtering happens entirely client-side — the backend returns every class
 * in the tenant in a single response. URL is still the source of truth for
 * the view state so deep-links and back-button flows work.
 */

export const classSortValues = [
  "name",
  "studentsDesc",
  "studentsAsc",
  "yearDesc",
] as const;
export type ClassSort = (typeof classSortValues)[number];

export const classStudentLoadValues = ["empty", "small", "medium", "large"] as const;
export type ClassStudentLoad = (typeof classStudentLoadValues)[number];

export const classFilterSchema = z.object({
  q: z.string().default(""),
  year: z.string().nullable().default(null),
  studentLoad: z.enum(classStudentLoadValues).nullable().default(null),
  sort: z.enum(classSortValues).default("name"),
});

export type ClassFilter = z.infer<typeof classFilterSchema>;

export const defaultClassFilter: ClassFilter = {
  q: "",
  year: null,
  studentLoad: null,
  sort: "name",
};

export function classFilterFromUrlParams(params: URLSearchParams): ClassFilter {
  const candidate = {
    q: params.get("q") ?? "",
    year: params.get("year"),
    studentLoad: params.get("load"),
    sort: params.get("sort") ?? "name",
  };
  const parsed = classFilterSchema.safeParse(candidate);
  return parsed.success ? parsed.data : defaultClassFilter;
}

export function classFilterToUrlParams(filter: ClassFilter): URLSearchParams {
  const params = new URLSearchParams();
  if (filter.q.trim()) params.set("q", filter.q.trim());
  if (filter.year) params.set("year", filter.year);
  if (filter.studentLoad) params.set("load", filter.studentLoad);
  if (filter.sort !== "name") params.set("sort", filter.sort);
  return params;
}

export function countActiveClassFilters(filter: ClassFilter): number {
  let count = 0;
  if (filter.q.trim()) count += 1;
  if (filter.year) count += 1;
  if (filter.studentLoad) count += 1;
  if (filter.sort !== "name") count += 1;
  return count;
}

export const classStudentLoadLabels: Record<ClassStudentLoad, string> = {
  empty: "Empty (0)",
  small: "Small (1–15)",
  medium: "Medium (16–30)",
  large: "Large (31+)",
};

export const classSortLabels: Record<ClassSort, string> = {
  name: "Name (A–Z)",
  studentsDesc: "Most students",
  studentsAsc: "Fewest students",
  yearDesc: "Newest academic year",
};

function matchesStudentLoad(count: number, bucket: ClassStudentLoad): boolean {
  switch (bucket) {
    case "empty":
      return count === 0;
    case "small":
      return count >= 1 && count <= 15;
    case "medium":
      return count >= 16 && count <= 30;
    case "large":
      return count >= 31;
    default:
      return true;
  }
}

/**
 * Applies the current filter to the full classes list and returns the
 * filtered + sorted subset the UI should render. Pure function; safe to
 * memoise in the caller.
 */
export function applyClassFilter(
  classes: readonly ClassItem[],
  filter: ClassFilter
): ClassItem[] {
  const needle = filter.q.trim().toLowerCase();
  const filtered = classes.filter((item) => {
    if (filter.year && item.academicYear !== filter.year) return false;
    if (filter.studentLoad && !matchesStudentLoad(item.studentCount, filter.studentLoad)) {
      return false;
    }
    if (needle) {
      const haystack = `${item.name} ${item.section} ${item.academicYear}`.toLowerCase();
      if (!haystack.includes(needle)) return false;
    }
    return true;
  });

  const sorted = [...filtered];
  switch (filter.sort) {
    case "studentsDesc":
      sorted.sort(
        (a, b) => b.studentCount - a.studentCount || a.name.localeCompare(b.name)
      );
      break;
    case "studentsAsc":
      sorted.sort(
        (a, b) => a.studentCount - b.studentCount || a.name.localeCompare(b.name)
      );
      break;
    case "yearDesc":
      sorted.sort(
        (a, b) =>
          b.academicYear.localeCompare(a.academicYear) ||
          a.name.localeCompare(b.name) ||
          a.section.localeCompare(b.section)
      );
      break;
    case "name":
    default:
      sorted.sort(
        (a, b) =>
          a.name.localeCompare(b.name) ||
          a.section.localeCompare(b.section) ||
          b.academicYear.localeCompare(a.academicYear)
      );
      break;
  }
  return sorted;
}

export interface ClassChipRemovalHandlers {
  removeSearch: () => void;
  removeYear: () => void;
  removeStudentLoad: () => void;
  removeSort: () => void;
}

export function buildClassFilterChips(
  filter: ClassFilter,
  handlers: ClassChipRemovalHandlers
): FilterChip[] {
  const chips: FilterChip[] = [];
  if (filter.q.trim()) {
    chips.push({
      key: "search",
      label: `Search: ${filter.q.trim()}`,
      onRemove: handlers.removeSearch,
    });
  }
  if (filter.year) {
    chips.push({
      key: "year",
      label: `Year: ${filter.year}`,
      onRemove: handlers.removeYear,
    });
  }
  if (filter.studentLoad) {
    chips.push({
      key: "load",
      label: `Students: ${classStudentLoadLabels[filter.studentLoad]}`,
      onRemove: handlers.removeStudentLoad,
    });
  }
  if (filter.sort !== "name") {
    chips.push({
      key: "sort",
      label: `Sort: ${classSortLabels[filter.sort]}`,
      onRemove: handlers.removeSort,
    });
  }
  return chips;
}
