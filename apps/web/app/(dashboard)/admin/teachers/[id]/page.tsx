"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost, apiDelete } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { ClassSelector } from "@/components/shared/class-selector";
import { ArrowLeft, Plus, Trash2 } from "lucide-react";
import type { TeacherProfile, AssignClassRequest } from "@/lib/types/teacher";
import type { ClassItem } from "@/lib/types/student";
import type { SubjectItem } from "@/lib/types/teacher";
import type { MutationResponse } from "@/lib/types/student";

export default function AdminTeacherDetailPage(): React.ReactElement {
  const params = useParams();
  const router = useRouter();
  const teacherId = params.id as string;

  const [teacher, setTeacher] = React.useState<TeacherProfile | null>(null);
  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [subjects, setSubjects] = React.useState<SubjectItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  // Assignment form
  const [showAssignForm, setShowAssignForm] = React.useState(false);
  const [assignClassId, setAssignClassId] = React.useState("");
  const [assignSubject, setAssignSubject] = React.useState("");
  const [isAssigning, setIsAssigning] = React.useState(false);
  const [assignError, setAssignError] = React.useState("");

  const [isRemoving, setIsRemoving] = React.useState<string | null>(null);

  const fetchTeacher = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const [teacherData, classData, subjectData] = await Promise.all([
        apiGet<TeacherProfile>(`${API_ENDPOINTS.teachers}/${teacherId}`),
        apiGet<ClassItem[]>(API_ENDPOINTS.classes),
        apiGet<SubjectItem[]>(API_ENDPOINTS.subjects),
      ]);
      setTeacher(teacherData);
      setClasses(classData);
      setSubjects(subjectData);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load teacher."
      );
    } finally {
      setIsLoading(false);
    }
  }, [teacherId]);

  React.useEffect(() => {
    fetchTeacher();
  }, [fetchTeacher]);

  const handleAssign = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setAssignError("");
    setSuccessMessage("");

    if (!assignClassId || !assignSubject) {
      setAssignError("Please select both a class and a subject.");
      return;
    }

    setIsAssigning(true);
    try {
      const body: AssignClassRequest = {
        classId: assignClassId,
        subject: assignSubject,
      };
      const result = await apiPost<MutationResponse>(
        `${API_ENDPOINTS.teachers}/${teacherId}/assignments`,
        body
      );
      setSuccessMessage(result.message);
      setShowAssignForm(false);
      setAssignClassId("");
      setAssignSubject("");
      fetchTeacher();
    } catch (err) {
      setAssignError(
        err instanceof ApiError ? err.message : "Failed to assign."
      );
    } finally {
      setIsAssigning(false);
    }
  };

  const handleRemove = async (
    assignmentId: string,
    label: string
  ): Promise<void> => {
    if (!confirm(`Remove assignment: ${label}?`)) return;

    setIsRemoving(assignmentId);
    setSuccessMessage("");
    try {
      await apiDelete<MutationResponse>(
        `${API_ENDPOINTS.teachers}/${teacherId}/assignments/${assignmentId}`
      );
      setSuccessMessage("Assignment removed.");
      fetchTeacher();
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to remove assignment."
      );
    } finally {
      setIsRemoving(null);
    }
  };

  const formatDate = (dateStr: string): string =>
    new Date(dateStr).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });

  if (isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  if (error && !teacher) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState title="Error" message={error} onRetry={fetchTeacher} />
      </div>
    );
  }

  if (!teacher) return <></>;

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push("/admin/teachers")}
          aria-label="Back to teachers"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <h1 className="text-3xl font-bold tracking-tight">
              {teacher.name}
            </h1>
            {!teacher.isActive && <Badge variant="destructive">Inactive</Badge>}
          </div>
          <p className="text-muted-foreground">{teacher.phone}</p>
        </div>
      </div>

      {successMessage && (
        <div className="rounded-md bg-green-50 p-3 text-sm text-green-800 dark:bg-green-950 dark:text-green-200">
          {successMessage}
        </div>
      )}

      {error && (
        <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Profile</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Name</span>
              <span className="text-sm font-medium">{teacher.name}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Phone</span>
              <span className="text-sm font-medium">{teacher.phone}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Joined</span>
              <span className="text-sm font-medium">
                {formatDate(teacher.createdAt)}
              </span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-lg">Assignments</CardTitle>
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                setShowAssignForm(!showAssignForm);
                setAssignError("");
              }}
            >
              <Plus className="mr-1 h-4 w-4" />
              Assign
            </Button>
          </CardHeader>
          <CardContent>
            {showAssignForm && (
              <form
                onSubmit={handleAssign}
                className="mb-4 space-y-3 rounded-md border bg-muted/30 p-3"
              >
                <ClassSelector
                  classes={classes}
                  value={assignClassId}
                  onChange={setAssignClassId}
                  disabled={isAssigning}
                  label="Class"
                />
                <div className="space-y-2">
                  <label
                    htmlFor="assignSubject"
                    className="block text-sm font-medium"
                  >
                    Subject
                  </label>
                  <select
                    id="assignSubject"
                    value={assignSubject}
                    onChange={(e) => setAssignSubject(e.target.value)}
                    disabled={isAssigning}
                    className="flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
                  >
                    <option value="" disabled>
                      Select a subject
                    </option>
                    {subjects.map((s) => (
                      <option key={s.id} value={s.name}>
                        {s.name}
                      </option>
                    ))}
                  </select>
                </div>
                {assignError && (
                  <p className="text-sm text-destructive">{assignError}</p>
                )}
                <div className="flex gap-2">
                  <Button type="submit" size="sm" disabled={isAssigning}>
                    {isAssigning ? <Spinner size="sm" /> : "Assign"}
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setShowAssignForm(false)}
                    disabled={isAssigning}
                  >
                    Cancel
                  </Button>
                </div>
              </form>
            )}

            {teacher.assignments.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No class assignments yet.
              </p>
            ) : (
              <ul className="space-y-2" aria-label="Teacher assignments">
                {teacher.assignments.map((a) => (
                  <li
                    key={a.assignmentId}
                    className="flex items-center justify-between rounded-md border p-3"
                  >
                    <div>
                      <p className="font-medium">
                        {a.className}
                        {a.section ? ` ${a.section}` : ""}
                      </p>
                      <p className="text-sm text-muted-foreground">
                        {a.subject}
                      </p>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() =>
                        handleRemove(
                          a.assignmentId,
                          `${a.className} ${a.section} — ${a.subject}`
                        )
                      }
                      disabled={isRemoving === a.assignmentId}
                      aria-label={`Remove ${a.subject} from ${a.className}`}
                      className="h-8 w-8 text-destructive hover:text-destructive"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
