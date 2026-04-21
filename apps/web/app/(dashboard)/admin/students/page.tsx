"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { useAuth } from "@/hooks/use-auth";
import { useStudentFilters } from "@/hooks/use-student-filters";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StudentCard } from "@/components/shared/student-card";
import { StudentFilterBar } from "@/components/students/filter-bar";
import {
  studentFilterToApiParams,
  type StudentFilter,
} from "@/lib/students/filter-schema";
import {
  ChevronLeft,
  ChevronRight,
  Plus,
  Users,
} from "lucide-react";
import type {
  ClassItem,
  PagedResult,
  StudentListItem,
} from "@/lib/types/student";

const PAGE_SIZE = 20;

export default function AdminStudentsPage(): React.ReactElement {
  const router = useRouter();
  const { token, isLoading: isAuthLoading } = useAuth();
  const { filters, setFilter, clearAll, activeCount } = useStudentFilters();

  const [students, setStudents] = React.useState<StudentListItem[]>([]);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const [searchValue, setSearchValue] = React.useState(filters.q);
  const [classOptions, setClassOptions] = React.useState<ClassItem[]>([]);
  const [classOptionsLoading, setClassOptionsLoading] = React.useState(true);

  const searchInputRef = React.useRef<HTMLInputElement | null>(null);

  // Debounce local search text → URL (filters.q).
  React.useEffect(() => {
    if (searchValue === filters.q) return;
    const timer = setTimeout(() => setFilter("q", searchValue), 300);
    return () => clearTimeout(timer);
  }, [searchValue, filters.q, setFilter]);

  // Reset to page 1 whenever the filter identity changes.
  React.useEffect(() => {
    setPage(1);
  }, [filters]);

  // Slash + Cmd/Ctrl+K shortcuts for search focus.
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

  const fetchStudents = React.useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError("");
    try {
      const params = studentFilterToApiParams(filters, filters.q, page, PAGE_SIZE);
      const data = await apiGet<PagedResult<StudentListItem>>(
        `${API_ENDPOINTS.students}?${params.toString()}`
      );
      setStudents(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load students.");
    } finally {
      setIsLoading(false);
    }
  }, [filters, page]);

  // Gate fetches on auth being resolved — mirrors the pre-existing
  // useStudentList hook behaviour.
  React.useEffect(() => {
    if (isAuthLoading || !token) return;
    void fetchStudents();
  }, [fetchStudents, isAuthLoading, token]);

  // Classes list is used for the multi-select options and chip labels.
  React.useEffect(() => {
    if (isAuthLoading || !token) return;
    let cancelled = false;
    apiGet<ClassItem[]>(API_ENDPOINTS.classes)
      .then((data) => {
        if (!cancelled) setClassOptions(data);
      })
      .catch(() => {
        // Silent: class filter is helpful but not required.
      })
      .finally(() => {
        if (!cancelled) setClassOptionsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [isAuthLoading, token]);

  const classFilterOptions = React.useMemo(
    () =>
      classOptions
        .map((c) => ({
          id: c.id,
          label: `Class ${c.name}${c.section ? ` ${c.section}` : ""} (${c.academicYear})`,
        }))
        .sort((a, b) => a.label.localeCompare(b.label)),
    [classOptions]
  );

  const handleFilterChange = React.useCallback(
    <K extends keyof StudentFilter>(key: K, value: StudentFilter[K]): void => {
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
        eyebrow="Students"
        title="Students"
        description="Manage student information and enrollments."
        icon={<Users className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <Button onClick={() => router.push("/admin/students/new")} size="sm">
            <Plus className="h-4 w-4" />
            Enroll Student
          </Button>
        )}
        stats={[
          { label: "Total", value: totalCount.toString() },
          { label: "Page", value: totalPages > 0 ? `${page}/${totalPages}` : "1/1" },
        ]}
      />

      <PageSection className="space-y-4">
        <StudentFilterBar
          filters={filters}
          searchValue={searchValue}
          onSearchChange={setSearchValue}
          onFilterChange={handleFilterChange}
          onClearAll={handleClearAll}
          activeCount={activeCount}
          classOptions={classFilterOptions}
          classOptionsLoading={classOptionsLoading}
          searchInputRef={searchInputRef}
        />

        {isLoading ? (
          <StudentListSkeleton />
        ) : error ? (
          <ErrorState title="Error" message={error} onRetry={fetchStudents} />
        ) : students.length === 0 ? (
          <EmptyState
            title={activeCount > 0 ? "No students match your filters" : "No students yet"}
            description={
              activeCount > 0
                ? "Try removing a filter or clearing all to broaden the search."
                : "Enroll your first student to get started."
            }
            icon={<Users className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
            action={
              activeCount > 0
                ? { label: "Clear filters", onClick: handleClearAll }
                : { label: "Enroll Student", onClick: () => router.push("/admin/students/new") }
            }
          />
        ) : (
          <>
            <p className="text-sm font-medium text-muted-foreground">
              {totalCount} student{totalCount !== 1 ? "s" : ""}
              {activeCount > 0 ? " match your filters" : ""}
            </p>
            <div className="grid gap-3 xl:grid-cols-2">
              {students.map((student) => (
                <StudentCard
                  key={student.id}
                  name={student.name}
                  rollNumber={student.rollNumber}
                  className={student.className}
                  section={student.section}
                  isActive={student.isActive}
                  showInactiveBadge
                  onClick={() => router.push(`/admin/students/${student.id}`)}
                />
              ))}
            </div>

            {totalPages > 1 && (
              <div className="flex flex-col gap-3 rounded-[22px] border border-border/70 bg-card/68 p-4 backdrop-blur-sm dark:bg-card/84 sm:flex-row sm:items-center sm:justify-between">
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
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                    aria-label="Next page"
                  >
                    Next
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

function StudentListSkeleton(): React.ReactElement {
  return (
    <div className="grid gap-3 xl:grid-cols-2" aria-busy="true" aria-live="polite">
      {Array.from({ length: 6 }).map((_, i) => (
        <div
          key={i}
          className="flex w-full items-center gap-3 rounded-[26px] border border-border/70 bg-card/60 p-4 shadow-[0_20px_50px_-40px_rgba(15,40,69,0.35)] dark:bg-card/80"
        >
          <div className="h-12 w-12 animate-pulse rounded-[18px] bg-muted/70" />
          <div className="flex-1 space-y-2">
            <div className="h-4 w-40 animate-pulse rounded bg-muted/70" />
            <div className="h-3 w-28 animate-pulse rounded bg-muted/50" />
          </div>
          <div className="h-6 w-16 animate-pulse rounded-full bg-muted/60" />
        </div>
      ))}
    </div>
  );
}
