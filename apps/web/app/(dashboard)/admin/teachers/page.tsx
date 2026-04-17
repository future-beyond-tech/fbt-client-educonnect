"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import {
  BookOpen,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type { TeacherListItem, TeacherPagedResult } from "@/lib/types/teacher";

type StaffRoleFilter = "all" | "teacher" | "admin";

export default function AdminTeachersPage(): React.ReactElement {
  const router = useRouter();
  const [teachers, setTeachers] = React.useState<TeacherListItem[]>([]);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [debouncedSearch, setDebouncedSearch] = React.useState("");
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [roleFilter, setRoleFilter] = React.useState<StaffRoleFilter>("all");

  const pageSize = 20;

  React.useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  const fetchTeachers = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (debouncedSearch) params.set("search", debouncedSearch);
      params.set("page", page.toString());
      params.set("pageSize", pageSize.toString());

      const data = await apiGet<TeacherPagedResult>(
        `${API_ENDPOINTS.teachers}?${params.toString()}`
      );
      setTeachers(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load teachers."
      );
    } finally {
      setIsLoading(false);
    }
  }, [debouncedSearch, page]);

  React.useEffect(() => {
    fetchTeachers();
  }, [fetchTeachers]);

  const visibleTeachers = React.useMemo(() => {
    if (roleFilter === "all") return teachers;
    if (roleFilter === "admin") return teachers.filter((t) => t.role === "Admin");
    return teachers.filter((t) => t.role === "Teacher");
  }, [roleFilter, teachers]);

  const roleCounts = React.useMemo(
    () => ({
      all: teachers.length,
      teacher: teachers.filter((t) => t.role === "Teacher").length,
      admin: teachers.filter((t) => t.role === "Admin").length,
    }),
    [teachers]
  );

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Staff"
        description="Manage teacher and admin accounts, search by name or phone, and review active assignments."
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              onClick={() => router.push("/admin/teachers/new")}
            >
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
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="w-full max-w-md">
            <Input
              placeholder="Search by name or phone..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search teachers"
            />
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {([
              { key: "all", label: `All (${roleCounts.all})` },
              { key: "teacher", label: `Teachers (${roleCounts.teacher})` },
              { key: "admin", label: `Admins (${roleCounts.admin})` },
            ] as const).map((option) => {
              const isActive = roleFilter === option.key;
              return (
                <button
                  key={option.key}
                  type="button"
                  onClick={() => setRoleFilter(option.key)}
                  aria-pressed={isActive}
                  className={cn(
                    "focus-ring inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-all",
                    isActive
                      ? "rainbow-bg border-transparent text-white shadow-[0_10px_24px_-16px_rgba(15,40,69,0.55)]"
                      : "border-border/70 bg-card/70 text-muted-foreground hover:border-primary/30 hover:text-foreground"
                  )}
                >
                  {option.label}
                </button>
              );
            })}
          </div>
        </div>

        {isLoading ? (
          <div className="flex min-h-96 items-center justify-center">
            <Spinner size="lg" />
          </div>
        ) : error ? (
          <ErrorState title="Error" message={error} onRetry={fetchTeachers} />
        ) : teachers.length === 0 ? (
          <EmptyState
            title="No staff found"
            description={
              debouncedSearch
                ? "Try adjusting your search."
                : "No teacher or admin accounts are registered in this school yet."
            }
            icon={
              <BookOpen
                className="h-8 w-8 text-muted-foreground"
                aria-hidden="true"
              />
            }
            action={
              !debouncedSearch
                ? {
                    label: "Add Staff",
                    onClick: () => router.push("/admin/teachers/new"),
                  }
                : undefined
            }
          />
        ) : visibleTeachers.length === 0 ? (
          <div className="rounded-[24px] border border-dashed border-border/70 bg-card/60 p-8 text-center shadow-[0_18px_46px_-34px_rgba(15,40,69,0.3)] dark:bg-card/80">
            <p className="text-sm font-medium text-foreground">
              No staff match the {roleFilter === "admin" ? "Admins" : "Teachers"} filter.
            </p>
            <p className="mt-1 text-sm text-muted-foreground">
              Switch to a different role filter to see more results.
            </p>
            <Button
              size="sm"
              variant="outline"
              onClick={() => setRoleFilter("all")}
              className="mt-4"
            >
              Show all
            </Button>
          </div>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              Showing {visibleTeachers.length} of {totalCount} staff account
              {totalCount !== 1 ? "s" : ""}
              {roleFilter !== "all" ? ` (${roleFilter === "admin" ? "admins" : "teachers"} only)` : ""}
            </p>
            <div className="space-y-3">
              {visibleTeachers.map((teacher) => (
                <button
                  key={teacher.id}
                  onClick={() =>
                    router.push(`/admin/teachers/${teacher.id}`)
                  }
                  className="flex w-full flex-col items-start gap-3 rounded-[26px] border border-border/70 bg-card/86 p-4 text-left shadow-[0_20px_50px_-40px_rgba(15,40,69,0.45)] transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 dark:bg-card/92 sm:flex-row sm:items-center sm:justify-between sm:gap-4"
                  aria-label={`Staff account ${teacher.name}`}
                >
                  <div className="min-w-0 w-full flex-1">
                    <p className="truncate text-base font-semibold text-foreground">
                      {teacher.name}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      {teacher.phone}
                    </p>
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
                    {!teacher.isActive && (
                      <Badge variant="destructive">Inactive</Badge>
                    )}
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
                    onClick={() =>
                      setPage((p) => Math.min(totalPages, p + 1))
                    }
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
