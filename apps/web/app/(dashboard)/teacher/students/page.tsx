"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { StudentListPage } from "@/components/shared/student-list-page";
import { useStudentList } from "@/hooks/use-student-list";

export default function TeacherStudentsPage(): React.ReactElement {
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
      description="View students in your assigned classes."
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
      onStudentSelect={(studentId) =>
        router.push(`/teacher/students/${studentId}`)
      }
      emptyDescription="No students are assigned to your classes yet."
      filteredEmptyDescription="Try adjusting your search or filter."
    />
  );
}
