"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { ClassSelector } from "@/components/shared/class-selector";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft } from "lucide-react";
import type {
  StudentDetail,
  ClassItem,
  UpdateStudentRequest,
  MutationResponse,
} from "@/lib/types/student";

export default function EditStudentPage(): React.ReactElement {
  const params = useParams();
  const router = useRouter();
  const studentId = params.id as string;

  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [name, setName] = React.useState("");
  const [classId, setClassId] = React.useState("");
  const [dateOfBirth, setDateOfBirth] = React.useState("");

  const [isLoading, setIsLoading] = React.useState(true);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");
  const [loadError, setLoadError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string>>(
    {}
  );

  React.useEffect(() => {
    const load = async (): Promise<void> => {
      setIsLoading(true);
      try {
        const [studentData, classData] = await Promise.all([
          apiGet<StudentDetail>(`${API_ENDPOINTS.students}/${studentId}`),
          apiGet<ClassItem[]>(API_ENDPOINTS.classes),
        ]);
        setName(studentData.name);
        setClassId(studentData.classId);
        setDateOfBirth(studentData.dateOfBirth ?? "");
        setClasses(classData);
      } catch (err) {
        setLoadError(
          err instanceof ApiError ? err.message : "Failed to load student."
        );
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [studentId]);

  const validate = (): boolean => {
    const errors: Record<string, string> = {};
    if (!name.trim()) errors.name = "Student name is required.";
    if (name.trim().length > 120)
      errors.name = "Name cannot exceed 120 characters.";
    if (!classId) errors.classId = "Please select a class.";
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError("");

    if (!validate()) return;

    setIsSubmitting(true);
    try {
      const body: UpdateStudentRequest = {
        name: name.trim(),
        classId,
        dateOfBirth: dateOfBirth || null,
      };
      await apiPut<MutationResponse>(
        `${API_ENDPOINTS.students}/${studentId}`,
        body
      );
      router.push(`/admin/students/${studentId}`);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to update student.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState
          title="Error"
          message={loadError}
          onRetry={() => window.location.reload()}
        />
      </div>
    );
  }

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Edit Student"
        description="Update student details, class placement, and core profile information."
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push(`/admin/students/${studentId}`)}
            aria-label="Back to student"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Student
          </Button>
        )}
      />

      <PageSection>
        <CardContent className="p-0">
          <form onSubmit={handleSubmit} className="max-w-2xl space-y-4">
            <Input
              label="Student Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.name}
            />

            <ClassSelector
              classes={classes}
              value={classId}
              onChange={setClassId}
              disabled={isSubmitting}
              error={fieldErrors.classId}
            />

            <Input
              label="Date of Birth (optional)"
              type="date"
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              disabled={isSubmitting}
            />

            {error && <StatusBanner variant="error">{error}</StatusBanner>}

            <div className="flex gap-2 pt-2">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? <Spinner size="sm" /> : "Save Changes"}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push(`/admin/students/${studentId}`)}
                disabled={isSubmitting}
              >
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </PageSection>
    </PageShell>
  );
}
