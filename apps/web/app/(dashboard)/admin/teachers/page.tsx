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
import {
  BookOpen,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import type { TeacherListItem, TeacherPagedResult } from "@/lib/types/teacher";

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

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Teachers</h1>
          <p className="text-muted-foreground">
            Manage teacher information and class assignments.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => router.push("/admin/subjects")}
        >
          Manage Subjects
        </Button>
      </div>

      <div className="max-w-md">
        <Input
          placeholder="Search by name or phone..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search teachers"
        />
      </div>

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchTeachers} />
      ) : teachers.length === 0 ? (
        <EmptyState
          title="No teachers found"
          description={
            debouncedSearch
              ? "Try adjusting your search."
              : "No teachers are registered in this school yet."
          }
          icon={
            <BookOpen
              className="h-8 w-8 text-muted-foreground"
              aria-hidden="true"
            />
          }
        />
      ) : (
        <>
          <p className="text-sm text-muted-foreground">
            {totalCount} teacher{totalCount !== 1 ? "s" : ""}
          </p>
          <div className="space-y-2">
            {teachers.map((teacher) => (
              <button
                key={teacher.id}
                onClick={() =>
                  router.push(`/admin/teachers/${teacher.id}`)
                }
                className="flex w-full items-center justify-between rounded-lg border bg-card p-4 text-left transition-colors hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                aria-label={`Teacher ${teacher.name}`}
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-foreground">
                    {teacher.name}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {teacher.phone}
                  </p>
                </div>
                <div className="ml-3 flex shrink-0 items-center gap-2">
                  {teacher.assignedClassCount > 0 ? (
                    <Badge variant="secondary">
                      {teacher.assignedClassCount} class
                      {teacher.assignedClassCount !== 1 ? "es" : ""}
                    </Badge>
                  ) : (
                    <Badge variant="outline">Unassigned</Badge>
                  )}
                  {teacher.subjects.length > 0 && (
                    <span className="text-xs text-muted-foreground">
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
    </div>
  );
}
