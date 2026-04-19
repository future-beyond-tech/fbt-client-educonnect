"use client";

import * as React from "react";
import { StudentEnrollmentForm } from "@/components/shared/student-enrollment-form";

export default function EnrollStudentPage(): React.ReactElement {
  return (
    <StudentEnrollmentForm
      eyebrow="Admin operations"
      title="Enroll Student"
      description="Create a new student profile and place them in the correct class."
      listHref="/admin/students"
      profileHref={(id) => `/admin/students/${id}`}
      manageClassesHref="/admin/classes"
    />
  );
}
