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
import { ClassSelector } from "@/components/shared/class-selector";
import { Users, ChevronLeft, ChevronRight } from "lucide-react";
import type {
  StudentListItem,
  PagedResult,
  ClassItem,
} from "@/lib/types/student";

export default function TeacherStudentsPage(): React.ReactElement {
  const router = useRouter();
  const [students, setStudents] = React.useState<StudentListItem[]>([]);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [selectedClassId, setSelectedClassId] = React.useState("");
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

  React.useEffect(() => {
    const fetchClasses = async (): Promise<void> => {
      try {
        const data = await apiGet<ClassItem[]>(API_ENDPOINTS.classes);
        setClasses(data);
      } catch {
        // Silently fail
      }
    };
    fetchClasses();
  }, []);

  const fetchStudents = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (selectedClassId) params.set("classId", selectedClassId);
      if (debouncedSearch) params.set("search", debouncedSearch);
      params.set("page", page.toString());
      params.set("pageSize", pageSize.toString());

      const data = await apiGet<PagedResult<StudentListItem>>(
        `${API_ENDPOINTS.students}?${params.toString()}`
      );
      setStudents(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load students."
      );
    } finally {
      setIsLoading(false);
    }
  }, [selectedClassId, debouncedSearch, page]);

  React.useEffect(() => {
    fetchStudents();
  }, [fetchStudents]);

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Students</h1>
        <p className="text-muted-foreground">
          View students in your assigned classes.
        </p>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row">
        <div className="flex-1">
          <Input
            placeholder="Search by name or roll number..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search students"
          />
        </div>
        <div className="w-full sm:w-64">
          <ClassSelector
            classes={classes}
            value={selectedClassId}
            onChange={(v) => {
              setSelectedClassId(v);
              setPage(1);
            }}
            includeAll
            label=""
          />
        </div>
      </div>

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchStudents} />
      ) : students.length === 0 ? (
        <EmptyState
          title="No students found"
          description={
            debouncedSearch || selectedClassId
              ? "Try adjusting your search or filter."
              : "No students are assigned to your classes yet."
          }
          icon={
            <Users
              className="h-8 w-8 text-muted-foreground"
              aria-hidden="true"
            />
          }
        />
      ) : (
        <>
          <p className="text-sm text-muted-foreground">
            {totalCount} student{totalCount !== 1 ? "s" : ""}
          </p>
          <div className="space-y-2">
            {students.map((student) => (
              <button
                key={student.id}
                onClick={() =>
                  router.push(`/teacher/students/${student.id}`)
                }
                className="flex w-full items-center justify-between rounded-lg border bg-card p-4 text-left transition-colors hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                aria-label={`Student ${student.name}, roll number ${student.rollNumber}`}
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-foreground">
                    {student.name}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    Roll: {student.rollNumber}
                  </p>
                </div>
                <Badge variant="secondary">
                  {student.className}
                  {student.section ? ` ${student.section}` : ""}
                </Badge>
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
