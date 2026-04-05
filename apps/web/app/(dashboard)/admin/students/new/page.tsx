"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ClassSelector } from "@/components/shared/class-selector";
import { ArrowLeft } from "lucide-react";
import type {
  ClassItem,
  EnrollStudentRequest,
  MutationResponse,
} from "@/lib/types/student";

export default function EnrollStudentPage(): React.ReactElement {
  const router = useRouter();
  const [classes, setClasses] = React.useState<ClassItem[]>([]);

  const [name, setName] = React.useState("");
  const [rollNumber, setRollNumber] = React.useState("");
  const [classId, setClassId] = React.useState("");
  const [dateOfBirth, setDateOfBirth] = React.useState("");

  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string>>(
    {}
  );

  React.useEffect(() => {
    const fetchClasses = async (): Promise<void> => {
      try {
        const data = await apiGet<ClassItem[]>(API_ENDPOINTS.classes);
        setClasses(data);
      } catch {
        setError("Failed to load classes.");
      }
    };
    fetchClasses();
  }, []);

  const validate = (): boolean => {
    const errors: Record<string, string> = {};
    if (!name.trim()) errors.name = "Student name is required.";
    if (name.trim().length > 120)
      errors.name = "Name cannot exceed 120 characters.";
    if (!rollNumber.trim()) errors.rollNumber = "Roll number is required.";
    if (!/^[A-Za-z0-9-]+$/.test(rollNumber.trim()))
      errors.rollNumber =
        "Roll number can only contain letters, numbers, and hyphens.";
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
      const body: EnrollStudentRequest = {
        name: name.trim(),
        rollNumber: rollNumber.trim(),
        classId,
        dateOfBirth: dateOfBirth || null,
      };
      const result = await apiPost<MutationResponse>(
        API_ENDPOINTS.students,
        body
      );
      router.push(`/admin/students/${result.studentId}`);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to enroll student.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push("/admin/students")}
          aria-label="Back to students"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Enroll Student</h1>
          <p className="text-muted-foreground">
            Add a new student to the school.
          </p>
        </div>
      </div>

      <Card>
        <CardContent className="p-4 md:p-6">
          <form onSubmit={handleSubmit} className="space-y-4 max-w-lg">
            <Input
              label="Student Name"
              placeholder="Enter student's full name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.name}
            />

            <Input
              label="Roll Number"
              placeholder="e.g. 2026-5A-001"
              value={rollNumber}
              onChange={(e) => setRollNumber(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.rollNumber}
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

            {error && <p className="text-sm text-destructive">{error}</p>}

            <div className="flex gap-2 pt-2">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? <Spinner size="sm" /> : "Enroll Student"}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push("/admin/students")}
                disabled={isSubmitting}
              >
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
