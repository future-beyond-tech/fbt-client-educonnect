"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { ArrowLeft } from "lucide-react";
import type { StudentDetail } from "@/lib/types/student";

export default function TeacherStudentDetailPage(): React.ReactElement {
  const params = useParams();
  const router = useRouter();
  const studentId = params.id as string;

  const [student, setStudent] = React.useState<StudentDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const fetchStudent = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<StudentDetail>(
        `${API_ENDPOINTS.students}/${studentId}`
      );
      setStudent(data);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load student."
      );
    } finally {
      setIsLoading(false);
    }
  }, [studentId]);

  React.useEffect(() => {
    fetchStudent();
  }, [fetchStudent]);

  const formatDate = (dateStr: string): string => {
    return new Date(dateStr).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  if (isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  if (error || !student) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState
          title="Error"
          message={error || "Student not found."}
          onRetry={fetchStudent}
        />
      </div>
    );
  }

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push("/teacher/students")}
          aria-label="Back to students"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-3xl font-bold tracking-tight">
              {student.name}
            </h1>
          </div>
          <p className="text-muted-foreground">
            Roll: {student.rollNumber} &middot; {student.className}
            {student.section ? ` ${student.section}` : ""}
          </p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Student Information</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="flex justify-between">
            <span className="text-sm text-muted-foreground">Full Name</span>
            <span className="text-sm font-medium">{student.name}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-sm text-muted-foreground">Roll Number</span>
            <span className="text-sm font-medium">{student.rollNumber}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-sm text-muted-foreground">Class</span>
            <span className="text-sm font-medium">
              {student.className}
              {student.section ? ` ${student.section}` : ""}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-sm text-muted-foreground">Academic Year</span>
            <span className="text-sm font-medium">
              {student.academicYear || "—"}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-sm text-muted-foreground">Date of Birth</span>
            <span className="text-sm font-medium">
              {student.dateOfBirth ? formatDate(student.dateOfBirth) : "—"}
            </span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
