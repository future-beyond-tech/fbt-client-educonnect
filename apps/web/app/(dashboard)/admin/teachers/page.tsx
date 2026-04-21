"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { TeacherFilterBar } from "@/components/teachers/filter-bar";
import {
  defaultTeacherFilter,
  teacherFilterToApiParams,
  type TeacherFilter,
} from "@/lib/teachers/filter-schema";
import { useTeacherFilters } from "@/hooks/use-teacher-filters";
import { BookOpen, ChevronLeft, ChevronRight } from "lucide-react";
import type { TeacherListItem, TeacherPagedResult } from "@/lib/types/teacher";

interface TeacherFilterMetadata {
  subjects: string[];
}

const PAGE_SIZE = 20;

export default function AdminTeachersPage(): React.ReactElement {
  const router = useRouter();
  const { filters, setFilter, clearAll, activeCount } = useTeacherFilters();

  const [teachers, setTeachers] = React.useState<TeacherListItem[]>([]);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const [searchValue, setSearchValue] = React.useState(filters.q);
  const [subjectOptions, setSubjectOptions] = React.useState<string[]>([]);
  const [subjectOptionsLoading, setSubjectOptionsLoading] = React.useState(true);

  const searchInputRef = React.useRef<HTMLInputElement | null>(null);

  // Debounce: local search text → URL (`filters.q`). Skip when already equal so
  // we don't thrash the router during the initial hydrate.
  React.useEffect(() => {
    if (searchValue === filters.q) return;
    const timer = setTimeout(() => setFilter("q", searchValue), 300);
    return () => clearTimeout(timer);
  }, [searchValue, filters.q, setFilter]);

  // Reset to page 1 whenever the filter identity changes — `filters` is memoised
  // in the hook and only changes when the URL does.
  React.useEffect(() => {
    setPage(1);
  }, [filters]);

  // "/" or "⌘K" focuses the search input. Skip when already typing in an input.
  React.useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      const target = event.target as HTMLElement | null;
      const isEditable =
        target?.tagName === "INPUT" ||
        target?.tagName === "TEXTAREA" ||
        target?.isContentEditable;
      if (isEditable) return;
      if (event.key === "/" || (event.key.toLowerCase() === "k" && (event.metaKey || event.ctrlKey))) {
        event.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const fetchTeachers = React.useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError("");
    try {
      const params = teacherFilterToApiParams(filters, filters.q, page, PAGE_SIZE);
      const data = await apiGet<TeacherPagedResult>(
        `${API_ENDPOINTS.teachers}?${params.toString()}`
      );
      setTeachers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load teachers.");
    } finally {
      setIsLoading(false);
    }
  }, [filters, page]);

  React.useEffect(() => {
    fetchTeachers();
  }, [fetchTeachers]);

  // Filter-metadata fetched once — subjects list doesn't change during the
  // page's lifetime unless the admin adds a new assignment elsewhere.
  React.useEffect(() => {
    let cancelled = false;
    apiGet<TeacherFilterMetadata>(`${API_ENDPOINTS.teachers}/filter-metadata`)
      .then((data) => {
        if (!cancelled) setSubjectOptions(data.subjects);
      })
      .catch(() => {
        // Fail silently — the filter bar still works without subject suggestions.
      })
      .finally(() => {
        if (!cancelled) setSubjectOptionsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Client-side role partitioning — role stays a UI-only filter in v1.
  const { teacherBucket, adminBucket, roleCounts } = React.useMemo(() => {
    const teacherB: TeacherListItem[] = [];
    const adminB: TeacherListItem[] = [];
    for (const t of teachers) {
      if (t.role === "Admin") adminB.push(t);
      else if (t.role === "Teacher") teacherB.push(t);
    }
    return {
      teacherBucket: teacherB,
      adminBucket: adminB,
      roleCounts: {
        all: teachers.length,
        teacher: teacherB.length,
        admin: adminB.length,
      },
    };
  }, [teachers]);

  const visibleTeachers = React.useMemo<TeacherListItem[]>(() => {
    if (filters.role === "all") return teachers;
    return filters.role === "admin" ? adminBucket : teacherBucket;
  }, [filters.role, teachers, teacherBucket, adminBucket]);

  const handleFilterChange = React.useCallback(
    <K extends keyof TeacherFilter>(key: K, value: TeacherFilter[K]): void => {
      setFilter(key, value);
    },
    [setFilter]
  );

  const handleClearAll = React.useCallback((): void => {
    setSearchValue("");
    clearAll();
  }, [clearAll]);

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Staff"
        description="Manage teacher and admin accounts, search by name or phone, and review active assignments."
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <div className="flex flex-wrap gap-2">
            <Button size="sm" onClick={() => router.push("/admin/teachers/new")}>
              Add Staff
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.push("/admin/classes")}
            >
              Manage Classes
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.push("/admin/subjects")}
            >
              Manage Subjects
            </Button>
          </div>
        )}
        stats={[
          { label: "Staff", value: totalCount.toString() },
          { label: "Page", value: totalPages > 0 ? `${page}/${totalPages}` : "1/1" },
        ]}
      />

      <PageSection className="space-y-4">
        <TeacherFilterBar
          filters={filters}
          searchValue={searchValue}
          onSearchChange={setSearchValue}
          onFilterChange={handleFilterChange}
          onClearAll={handleClearAll}
          activeCount={activeCount}
          roleCounts={roleCounts}
          subjectOptions={subjectOptions}
          subjectOptionsLoading={subjectOptionsLoading}
          includeCreatedSort={true}
          searchInputRef={searchInputRef}
        />

        {isLoading ? (
          <TeacherListSkeleton />
        ) : error ? (
          <ErrorState title="Error" message={error} onRetry={fetchTeachers} />
        ) : teachers.length === 0 ? (
          <EmptyState
            title={activeCount > 0 ? "No staff match your filters" : "No staff found"}
            description={
              activeCount > 0
                ? "Try removing a filter or clearing all to broaden the search."
                : "No teacher or admin accounts are registered in this school yet."
            }
            icon={<BookOpen className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
            action={
              activeCount > 0
                ? { label: "Clear filters", onClick: handleClearAll }
                : { label: "Add Staff", onClick: () => router.push("/admin/teachers/new") }
            }
          />
        ) : visibleTeachers.length === 0 ? (
          <EmptyState
            title="No staff match the role filter"
            description={`Switch roles or clear the filter to see the ${teachers.length} result${teachers.length === 1 ? "" : "s"} on this page.`}
            icon={<BookOpen className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
            action={{
              label: "Show all roles",
              onClick: () => setFilter("role", defaultTeacherFilter.role),
            }}
          />
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              Showing {visibleTeachers.length} of {totalCount} staff account
              {totalCount !== 1 ? "s" : ""}
              {filters.role !== "all"
                ? ` (${filters.role === "admin" ? "admins" : "teachers"} only)`
                : ""}
            </p>
            <div className="space-y-3">
              {visibleTeachers.map((teacher) => (
                <button
                  key={teacher.id}
                  onClick={() => router.push(`/admin/teachers/${teacher.id}`)}
                  className="flex w-full flex-col items-start gap-3 rounded-[26px] border border-border/70 bg-card/86 p-4 text-left shadow-[0_20px_50px_-40px_rgba(15,40,69,0.45)] transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 dark:bg-card/92 sm:flex-row sm:items-center sm:justify-between sm:gap-4"
                  aria-label={`Staff account ${teacher.name}`}
                >
                  <div className="min-w-0 w-full flex-1">
                    <p className="truncate text-base font-semibold text-foreground">
                      {teacher.name}
                    </p>
                    <p className="text-sm text-muted-foreground">{teacher.phone}</p>
                  </div>
                  <div className="flex w-full flex-wrap items-center justify-start gap-2 sm:ml-3 sm:w-auto sm:shrink-0 sm:justify-end">
                    <Badge variant={teacher.role === "Admin" ? "default" : "secondary"}>
                      {teacher.role}
                    </Badge>
                    {teacher.role === "Teacher" && teacher.assignedClassCount > 0 ? (
                      <Badge variant="secondary">
                        {teacher.assignedClassCount} class
                        {teacher.assignedClassCount !== 1 ? "es" : ""}
                      </Badge>
                    ) : teacher.role === "Teacher" ? (
                      <Badge variant="outline">Unassigned</Badge>
                    ) : (
                      <Badge variant="outline">School access</Badge>
                    )}
                    {teacher.role === "Teacher" && teacher.subjects.length > 0 && (
                      <span className="w-full text-xs text-muted-foreground sm:w-auto">
                        {teacher.subjects.slice(0, 3).join(", ")}
                        {teacher.subjects.length > 3 ? "..." : ""}
                      </span>
                    )}
                    {!teacher.isActive && <Badge variant="destructive">Inactive</Badge>}
                  </div>
                </button>
              ))}
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-2">
                <p className="text-sm text-muted-foreground">
                  Page {page} of {totalPages}
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                    aria-label="Previous page"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                    aria-label="Next page"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </PageSection>
    </PageShell>
  );
}

function TeacherListSkeleton(): React.ReactElement {
  return (
    <div className="space-y-3" aria-busy="true" aria-live="polite">
      {Array.from({ length: 4 }).map((_, i) => (
        <div
          key={i}
          className="flex w-full items-center gap-3 rounded-[26px] border border-border/70 bg-card/60 p-4 shadow-[0_20px_50px_-40px_rgba(15,40,69,0.35)] dark:bg-card/80"
        >
          <div className="flex-1 space-y-2">
            <div className="h-4 w-40 animate-pulse rounded bg-muted/70" />
            <div className="h-3 w-28 animate-pulse rounded bg-muted/50" />
          </div>
          <div className="flex gap-2">
            <div className="h-6 w-16 animate-pulse rounded-full bg-muted/60" />
            <div className="h-6 w-20 animate-pulse rounded-full bg-muted/50" />
          </div>
        </div>
      ))}
    </div>
  );
}
