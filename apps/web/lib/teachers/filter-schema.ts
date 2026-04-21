import { z } from "zod";

/**
 * Single source of truth for the staff filter bar.
 *
 * The same schema drives:
 *   - URL parsing (`useTeacherFilters` reads `?q=&role=&subjects=&load=&sort=`)
 *   - Outbound API query params (handler accepts `search/subjects/classLoad/sortBy`)
 *   - Form-control default values
 *
 * URL + API values use the same identifiers so there's no translation surface
 * except for the three field renames (`q → search`, `load → classLoad`,
 * `sort → sortBy`). Role filter is applied client-side — see the PR description.
 */

export const teacherRoleValues = ["all", "teacher", "admin"] as const;
export type TeacherRoleFilter = (typeof teacherRoleValues)[number];

export const teacherClassLoadValues = ["unassigned", "light", "heavy"] as const;
export type TeacherClassLoad = (typeof teacherClassLoadValues)[number];

export const teacherSortValues = [
  "nameAsc",
  "nameDesc",
  "classesDesc",
  "classesAsc",
  "createdDesc",
] as const;
export type TeacherSort = (typeof teacherSortValues)[number];

export const teacherFilterSchema = z.object({
  q: z.string().default(""),
  role: z.enum(teacherRoleValues).default("all"),
  subjects: z.array(z.string().min(1)).default([]),
  load: z.enum(teacherClassLoadValues).nullable().default(null),
  sort: z.enum(teacherSortValues).default("nameAsc"),
});

export type TeacherFilter = z.infer<typeof teacherFilterSchema>;

export const defaultTeacherFilter: TeacherFilter = {
  q: "",
  role: "all",
  subjects: [],
  load: null,
  sort: "nameAsc",
};

export function teacherFilterFromUrlParams(params: URLSearchParams): TeacherFilter {
  const subjectsRaw = params.get("subjects") ?? "";
  const candidate = {
    q: params.get("q") ?? "",
    role: params.get("role") ?? "all",
    subjects: subjectsRaw
      ? subjectsRaw
          .split(",")
          .map((s) => s.trim())
          .filter((s) => s.length > 0)
      : [],
    load: params.get("load"),
    sort: params.get("sort") ?? "nameAsc",
  };
  const parsed = teacherFilterSchema.safeParse(candidate);
  return parsed.success ? parsed.data : defaultTeacherFilter;
}

/**
 * Serialises the filter to URL params, omitting default values so that the
 * no-op URL stays clean (`/admin/teachers`, not `/admin/teachers?q=&role=all&...`).
 */
export function teacherFilterToUrlParams(filter: TeacherFilter): URLSearchParams {
  const params = new URLSearchParams();
  if (filter.q.trim()) params.set("q", filter.q.trim());
  if (filter.role !== "all") params.set("role", filter.role);
  if (filter.subjects.length > 0) params.set("subjects", filter.subjects.join(","));
  if (filter.load) params.set("load", filter.load);
  if (filter.sort !== "nameAsc") params.set("sort", filter.sort);
  return params;
}

/**
 * Params sent to `GET /api/teachers`. Role is deliberately excluded — the
 * backend returns both Teachers and Admins and the UI filters client-side.
 */
export function teacherFilterToApiParams(
  filter: TeacherFilter,
  debouncedSearch: string,
  page: number,
  pageSize: number
): URLSearchParams {
  const params = new URLSearchParams();
  if (debouncedSearch.trim()) params.set("search", debouncedSearch.trim());
  if (filter.subjects.length > 0) params.set("subjects", filter.subjects.join(","));
  if (filter.load) params.set("classLoad", filter.load);
  if (filter.sort !== "nameAsc") params.set("sortBy", filter.sort);
  params.set("page", page.toString());
  params.set("pageSize", pageSize.toString());
  return params;
}

export function countActiveFilters(filter: TeacherFilter): number {
  let count = 0;
  if (filter.q.trim()) count += 1;
  if (filter.role !== "all") count += 1;
  if (filter.subjects.length > 0) count += 1;
  if (filter.load) count += 1;
  if (filter.sort !== "nameAsc") count += 1;
  return count;
}

export const teacherClassLoadLabels: Record<TeacherClassLoad, string> = {
  unassigned: "Unassigned (0)",
  light: "Light (1–2)",
  heavy: "Heavy (3+)",
};

export const teacherSortLabels: Record<TeacherSort, string> = {
  nameAsc: "Name A→Z",
  nameDesc: "Name Z→A",
  classesDesc: "Most classes",
  classesAsc: "Fewest classes",
  createdDesc: "Recently added",
};
