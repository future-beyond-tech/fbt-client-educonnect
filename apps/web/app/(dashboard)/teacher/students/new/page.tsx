"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { apiGet, ApiError } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { StudentEnrollmentForm } from "@/components/shared/student-enrollment-form";
import {
  PageHeader,
  PageSection,
  PageShell,
} from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { ArrowLeft } from "lucide-react";
import type { ClassItem } from "@/lib/types/student";
import type { TeacherClassItem } from "@/lib/types/teacher";

/**
 * Teacher self-service enrollment.
 *
 * A teacher's /api/teachers/my-classes response includes every assignment —
 * subject-teacher AND class-teacher. Enrollment is gated to class-teacher
 * assignments only (matching the backend guard in
 * EnrollStudentCommandHandler), so we filter here. Multiple assignments can
 * share the same class (e.g. a class teacher who also teaches another
 * subject there), so we dedupe by classId before handing off to the form.
 */
function mapToClassItems(teacherClasses: TeacherClassItem[]): ClassItem[] {
  const seen = new Set<string>();
  const result: ClassItem[] = [];

  for (const tc of teacherClasses) {
    if (!tc.isClassTeacher) continue;
    if (seen.has(tc.classId)) continue;
    seen.add(tc.classId);
    result.push({
      id: tc.classId,
      name: tc.className,
      section: tc.section,
      // my-classes doesn't surface academic year or student count, and the
      // shared form never displays them — safe defaults are fine.
      academicYear: "",
      studentCount: 0,
    });
  }

  return result;
}

export default function TeacherEnrollStudentPage(): React.ReactElement {
  const router = useRouter();
  const [classes, setClasses] = React.useState<ClassItem[] | null>(null);
  const [loadError, setLoadError] = React.useState("");

  React.useEffect(() => {
    let cancelled = false;

    const load = async (): Promise<void> => {
      try {
        const data = await apiGet<TeacherClassItem[]>(
          API_ENDPOINTS.teachersMyClasses
        );
        if (cancelled) return;
        setClasses(mapToClassItems(data));
      } catch (err) {
        if (cancelled) return;
        setLoadError(
          err instanceof ApiError
            ? err.message
            : "Failed to load your assigned classes."
        );
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  if (loadError) {
    return (
      <PageShell>
        <PageHeader
          eyebrow="Teacher tools"
          title="Enroll Student"
          description="Add a new student to your class."
          backAction={(
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.push("/teacher/students")}
              aria-label="Back to students"
            >
              <ArrowLeft className="h-4 w-4" />
              Back to Students
            </Button>
          )}
        />
        <PageSection>
          <CardContent className="p-0">
            <StatusBanner variant="error">{loadError}</StatusBanner>
          </CardContent>
        </PageSection>
      </PageShell>
    );
  }

  if (classes === null) {
    return (
      <PageShell>
        <PageHeader
          eyebrow="Teacher tools"
          title="Enroll Student"
          description="Add a new student to your class."
        />
        <PageSection>
          <CardContent className="flex items-center justify-center p-8">
            <Spinner />
          </CardContent>
        </PageSection>
      </PageShell>
    );
  }

  return (
    <StudentEnrollmentForm
      classes={classes}
      eyebrow="Teacher tools"
      title="Enroll Student"
      description="Add a new student to your class. You can enroll into classes where you are the class teacher."
      listHref="/teacher/students"
      profileHref={(id) => `/teacher/students/${id}`}
      emptyClassesMessage="You are not listed as a class teacher for any class yet. Ask your administrator to assign you as the class teacher before enrolling students."
    />
  );
}
