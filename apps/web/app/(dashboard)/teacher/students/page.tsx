"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { UserPlus } from "lucide-react";
import { apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { StudentListPage } from "@/components/shared/student-list-page";
import { useStudentList } from "@/hooks/use-student-list";
import type { TeacherClassItem } from "@/lib/types/teacher";

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

  // The Enroll Student action is gated on the teacher actually being a class
  // teacher for at least one class. Subject-teacher assignments alone don't
  // unlock enrollment (the backend enforces the same rule in
  // EnrollStudentCommandHandler), so showing the button without that check
  // would invite a 403.
  const [canEnroll, setCanEnroll] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;

    const loadEnrollmentCapability = async (): Promise<void> => {
      try {
        const data = await apiGet<TeacherClassItem[]>(
          API_ENDPOINTS.teachersMyClasses
        );
        if (cancelled) return;
        setCanEnroll(data.some((c) => c.isClassTeacher));
      } catch {
        // Silently fall back to hiding the action — the main students list
        // will already have surfaced any auth/network failure via its own
        // error state, so we don't want to double-notify the user.
        if (cancelled) return;
        setCanEnroll(false);
      }
    };

    void loadEnrollmentCapability();

    return () => {
      cancelled = true;
    };
  }, []);

  const headerAction = canEnroll ? (
    <Button
      type="button"
      onClick={() => router.push("/teacher/students/new")}
    >
      <UserPlus className="h-4 w-4" />
      Enroll Student
    </Button>
  ) : undefined;

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
      headerAction={headerAction}
    />
  );
}
