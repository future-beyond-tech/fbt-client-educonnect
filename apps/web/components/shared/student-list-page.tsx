"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight, Search, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { ClassSelector } from "@/components/shared/class-selector";
import { EmptyState, type EmptyStateProps } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { StudentCard } from "@/components/shared/student-card";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import type { ClassItem, StudentListItem } from "@/lib/types/student";

export interface StudentListPageProps {
  title: string;
  description: string;
  students: StudentListItem[];
  totalCount: number;
  totalPages: number;
  page: number;
  classes: ClassItem[];
  selectedClassId: string;
  search: string;
  isLoading: boolean;
  error: string;
  hasActiveFilters: boolean;
  onSearchChange: (value: string) => void;
  onClassChange: (classId: string) => void;
  onPageChange: React.Dispatch<React.SetStateAction<number>>;
  onRetry: () => void;
  onStudentSelect: (studentId: string) => void;
  emptyDescription: string;
  filteredEmptyDescription: string;
  headerAction?: React.ReactNode;
  emptyAction?: EmptyStateProps["action"];
  showInactiveBadge?: boolean;
  resultSuffix?: string;
}

export function StudentListPage({
  title,
  description,
  students,
  totalCount,
  totalPages,
  page,
  classes,
  selectedClassId,
  search,
  isLoading,
  error,
  hasActiveFilters,
  onSearchChange,
  onClassChange,
  onPageChange,
  onRetry,
  onStudentSelect,
  emptyDescription,
  filteredEmptyDescription,
  headerAction,
  emptyAction,
  showInactiveBadge = false,
  resultSuffix = "",
}: StudentListPageProps): React.ReactElement {
  const studentCountLabel = `${totalCount} student${
    totalCount !== 1 ? "s" : ""
  }${resultSuffix}`;

  return (
    <PageShell>
      <PageHeader
        eyebrow="Students"
        title={title}
        description={description}
        actions={headerAction}
        icon={<Users className="h-6 w-6" aria-hidden="true" />}
        stats={[
          { label: "Total", value: totalCount.toString() },
          {
            label: "Filters",
            value: hasActiveFilters ? "Active" : "All classes",
          },
        ]}
      />

      <PageSection className="space-y-4">
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_260px]">
          <div className="relative">
            <Search
              className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <Input
              placeholder="Search by name or roll number..."
              value={search}
              onChange={(event) => onSearchChange(event.target.value)}
              aria-label="Search students"
              className="pl-11"
            />
          </div>
          <ClassSelector
            classes={classes}
            value={selectedClassId}
            onChange={onClassChange}
            includeAll
            label=""
          />
        </div>

        {isLoading ? (
          <div className="flex min-h-96 items-center justify-center">
            <Spinner size="lg" />
          </div>
        ) : error ? (
          <ErrorState title="Error" message={error} onRetry={onRetry} />
        ) : students.length === 0 ? (
          <EmptyState
            title="No students found"
            description={
              hasActiveFilters ? filteredEmptyDescription : emptyDescription
            }
            icon={
              <Users
                className="h-8 w-8 text-muted-foreground"
                aria-hidden="true"
              />
            }
            action={!hasActiveFilters ? emptyAction : undefined}
          />
        ) : (
          <>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm font-medium text-muted-foreground">
                {studentCountLabel}
              </p>
              {totalPages > 1 && (
                <p className="text-sm text-muted-foreground">
                  Page {page} of {totalPages}
                </p>
              )}
            </div>

            <div className="grid gap-3 xl:grid-cols-2">
              {students.map((student) => (
                <StudentCard
                  key={student.id}
                  name={student.name}
                  rollNumber={student.rollNumber}
                  className={student.className}
                  section={student.section}
                  isActive={student.isActive}
                  showInactiveBadge={showInactiveBadge}
                  onClick={() => onStudentSelect(student.id)}
                />
              ))}
            </div>

            {totalPages > 1 && (
              <div className="flex flex-col gap-3 rounded-[22px] border border-border/70 bg-card/68 p-4 backdrop-blur-sm dark:bg-card/84 sm:flex-row sm:items-center sm:justify-between">
                <p className="text-sm text-muted-foreground">
                  Keep moving through the roster without losing your filters.
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => onPageChange((current) => Math.max(1, current - 1))}
                    disabled={page <= 1}
                    aria-label="Previous page"
                  >
                    <ChevronLeft className="h-4 w-4" />
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      onPageChange((current) => Math.min(totalPages, current + 1))
                    }
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
