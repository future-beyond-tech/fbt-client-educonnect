"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { StudentListPage } from "@/components/shared/student-list-page";
import { useStudentList } from "@/hooks/use-student-list";
import { Plus } from "lucide-react";

export default function AdminStudentsPage(): React.ReactElement {
  const router = useRouter();
  const {
    students,
    totalCount,
    totalPages,
    page,
    classes,
    selectedClassId,
    search,
    hasActiveFilters,
    isLoading,
    error,
    setSearch,
    setPage,
    handleClassChange,
    fetchStudents,
  } = useStudentList();

  return (
    <StudentListPage
      title="Students"
      description="Manage student information and enrollments."
      students={students}
      totalCount={totalCount}
      totalPages={totalPages}
      page={page}
      classes={classes}
      selectedClassId={selectedClassId}
      search={search}
      isLoading={isLoading}
      error={error}
      hasActiveFilters={hasActiveFilters}
      onSearchChange={setSearch}
      onClassChange={handleClassChange}
      onPageChange={setPage}
      onRetry={fetchStudents}
      onStudentSelect={(studentId) => router.push(`/admin/students/${studentId}`)}
      emptyDescription="Enroll your first student to get started."
      filteredEmptyDescription="Try adjusting your search or filter."
      headerAction={
        <Button onClick={() => router.push("/admin/students/new")} size="sm">
          <Plus className="mr-1 h-4 w-4" />
          Enroll Student
        </Button>
      }
      emptyAction={{
        label: "Enroll Student",
        onClick: () => router.push("/admin/students/new"),
      }}
      showInactiveBadge
      resultSuffix=" found"
    />
  );
}
