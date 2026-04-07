"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { ClassSelector } from "@/components/shared/class-selector";
import { EmptyState, type EmptyStateProps } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { StudentCard } from "@/components/shared/student-card";
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
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
          <p className="text-muted-foreground">{description}</p>
        </div>
        {headerAction}
      </div>

      <div className="flex flex-col gap-3 sm:flex-row">
        <div className="flex-1">
          <Input
            placeholder="Search by name or roll number..."
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            aria-label="Search students"
          />
        </div>
        <div className="w-full sm:w-64">
          <ClassSelector
            classes={classes}
            value={selectedClassId}
            onChange={onClassChange}
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
          <p className="text-sm text-muted-foreground">{studentCountLabel}</p>

          <div className="space-y-2">
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
            <div className="flex items-center justify-between pt-2">
              <p className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
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
