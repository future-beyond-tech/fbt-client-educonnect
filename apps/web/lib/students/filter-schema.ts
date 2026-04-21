import { z } from "zod";
import type { FilterChip } from "@/components/ui/active-filter-chips";

/**
 * Single source of truth for the admin Students filter bar — mirrors the
 * staff filter pattern (see lib/teachers/filter-schema.ts).
 *
 * Role scope: this schema powers the admin view only. The teacher's own
 * Students page still uses the simpler shared useStudentList hook.
 */

export const studentStatusValues = ["active", "inactive"] as const;
export type StudentStatus = (typeof studentStatusValues)[number];

export const studentSortValues = [
  "default",
  "nameAsc",
  "nameDesc",
  "rollAsc",
  "createdDesc",
] as const;
export type StudentSort = (typeof studentSortValues)[number];

export const studentFilterSchema = z.object({
  q: z.string().default(""),
  classIds: z.array(z.string().min(1)).default([]),
  status: z.enum(studentStatusValues).nullable().default(null),
  sort: z.enum(studentSortValues).default("default"),
});

export type StudentFilter = z.infer<typeof studentFilterSchema>;

export const defaultStudentFilter: StudentFilter = {
  q: "",
  classIds: [],
  status: null,
  sort: "default",
};

export function studentFilterFromUrlParams(params: URLSearchParams): StudentFilter {
  const classIdsRaw = params.get("classIds") ?? "";
  const candidate = {
    q: params.get("q") ?? "",
    classIds: classIdsRaw
      ? classIdsRaw
          .split(",")
          .map((s) => s.trim())
          .filter((s) => s.length > 0)
      : [],
    status: params.get("status"),
    sort: params.get("sort") ?? "default",
  };
  const parsed = studentFilterSchema.safeParse(candidate);
  return parsed.success ? parsed.data : defaultStudentFilter;
}

export function studentFilterToUrlParams(filter: StudentFilter): URLSearchParams {
  const params = new URLSearchParams();
  if (filter.q.trim()) params.set("q", filter.q.trim());
  if (filter.classIds.length > 0) params.set("classIds", filter.classIds.join(","));
  if (filter.status) params.set("status", filter.status);
  if (filter.sort !== "default") params.set("sort", filter.sort);
  return params;
}

export function studentFilterToApiParams(
  filter: StudentFilter,
  debouncedSearch: string,
  page: number,
  pageSize: number
): URLSearchParams {
  const params = new URLSearchParams();
  if (debouncedSearch.trim()) params.set("search", debouncedSearch.trim());
  if (filter.classIds.length > 0) params.set("classIds", filter.classIds.join(","));
  if (filter.status) params.set("status", filter.status);
  if (filter.sort !== "default") params.set("sortBy", filter.sort);
  params.set("page", page.toString());
  params.set("pageSize", pageSize.toString());
  return params;
}

export function countActiveStudentFilters(filter: StudentFilter): number {
  let count = 0;
  if (filter.q.trim()) count += 1;
  if (filter.classIds.length > 0) count += 1;
  if (filter.status) count += 1;
  if (filter.sort !== "default") count += 1;
  return count;
}

export const studentStatusLabels: Record<StudentStatus, string> = {
  active: "Active only",
  inactive: "Inactive only",
};

export const studentSortLabels: Record<StudentSort, string> = {
  default: "Class & roll",
  nameAsc: "Name A→Z",
  nameDesc: "Name Z→A",
  rollAsc: "Roll number",
  createdDesc: "Recently enrolled",
};

export interface StudentChipRemovalHandlers {
  removeSearch: () => void;
  removeClass: (classId: string) => void;
  removeStatus: () => void;
  removeSort: () => void;
}

export function buildStudentFilterChips(
  filter: StudentFilter,
  /** Map of classId → human label, for nicer chips than raw GUIDs. */
  classLabels: ReadonlyMap<string, string>,
  handlers: StudentChipRemovalHandlers
): FilterChip[] {
  const chips: FilterChip[] = [];
  if (filter.q.trim()) {
    chips.push({
      key: "search",
      label: `Search: ${filter.q.trim()}`,
      onRemove: handlers.removeSearch,
    });
  }
  for (const classId of filter.classIds) {
    chips.push({
      key: `class:${classId}`,
      label: `Class: ${classLabels.get(classId) ?? "Unknown"}`,
      onRemove: () => handlers.removeClass(classId),
    });
  }
  if (filter.status) {
    chips.push({
      key: "status",
      label: `Status: ${studentStatusLabels[filter.status]}`,
      onRemove: handlers.removeStatus,
    });
  }
  if (filter.sort !== "default") {
    chips.push({
      key: "sort",
      label: `Sort: ${studentSortLabels[filter.sort]}`,
      onRemove: handlers.removeSort,
    });
  }
  return chips;
}
