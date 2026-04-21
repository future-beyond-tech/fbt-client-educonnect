"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import {
  assignClassToTeacherAction,
  promoteClassTeacherAction,
  removeTeacherAssignmentAction,
} from "@/lib/actions/users-actions";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { ClassSelector } from "@/components/shared/class-selector";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { Select } from "@/components/ui/select";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft, Plus, UserMinus } from "lucide-react";
import type { TeacherProfile } from "@/lib/types/teacher";
import type { ClassItem } from "@/lib/types/student";
import type { SubjectItem } from "@/lib/types/teacher";

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
  const [assignIsClassTeacher, setAssignIsClassTeacher] = React.useState(false);
  const [isAssigning, setIsAssigning] = React.useState(false);
  const [assignError, setAssignError] = React.useState("");

  const [isRemoving, setIsRemoving] = React.useState<string | null>(null);
  const [isPromoting, setIsPromoting] = React.useState<string | null>(null);

  const getBestErrorMessage = React.useCallback((err: unknown, fallback: string): string => {
    if (!(err instanceof ApiError)) return fallback;

    const firstValidationMessage = err.details?.errors
      ? Object.values(err.details.errors).flat()[0]
      : undefined;

    return firstValidationMessage || err.message || fallback;
  }, []);

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
      setError(getBestErrorMessage(err, "Failed to load teacher."));
    } finally {
      setIsLoading(false);
    }
  }, [getBestErrorMessage, teacherId]);

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
      const result = await assignClassToTeacherAction({
        teacherId,
        classId: assignClassId,
        subject: assignSubject,
        isClassTeacher: assignIsClassTeacher,
      });
      if (!result.ok) {
        setAssignError(
          result.formError ??
            Object.values(result.fieldErrors ?? {})[0] ??
            "Failed to assign.",
        );
        return;
      }
      setSuccessMessage(result.data.message);
      setShowAssignForm(false);
      setAssignClassId("");
      setAssignSubject("");
      setAssignIsClassTeacher(false);
      fetchTeacher();
    } catch {
      setAssignError("Failed to assign.");
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
      const result = await removeTeacherAssignmentAction(teacherId, assignmentId);
      if (!result.ok) {
        setError(result.formError ?? "Failed to remove assignment.");
        return;
      }
      setSuccessMessage("Assignment removed.");
      fetchTeacher();
    } catch {
      setError("Failed to remove assignment.");
    } finally {
      setIsRemoving(null);
    }
  };

  const handlePromoteClassTeacher = async (assignmentId: string): Promise<void> => {
    setIsPromoting(assignmentId);
    setError("");
    setSuccessMessage("");
    try {
      const result = await promoteClassTeacherAction(teacherId, assignmentId);
      if (!result.ok) {
        setError(result.formError ?? "Failed to update class teacher.");
        return;
      }
      setSuccessMessage(result.data.message);
      fetchTeacher();
    } catch {
      setError("Failed to update class teacher.");
    } finally {
      setIsPromoting(null);
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
  const isTeacherAccount = teacher.role === "Teacher";

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title={teacher.name}
        description={`${teacher.role} account • ${teacher.phone}`}
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push("/admin/teachers")}
            aria-label="Back to staff"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Staff
          </Button>
        )}
        actions={isTeacherAccount ? (
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setShowAssignForm(!showAssignForm);
              setAssignError("");
            }}
          >
            <Plus className="h-4 w-4" />
            Assign
          </Button>
        ) : undefined}
        stats={[
          { label: "Role", value: teacher.role },
          { label: "Assignments", value: teacher.assignments.length.toString() },
          { label: "Joined", value: formatDate(teacher.createdAt) },
        ]}
      />

      {!teacher.isActive && (
        <StatusBanner variant="warning">This {teacher.role.toLowerCase()} account is currently inactive.</StatusBanner>
      )}
      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}
      {error && (
        <StatusBanner variant="error">{error}</StatusBanner>
      )}

      <div className="grid gap-4 xl:grid-cols-2">
        <PageSection>
          <CardHeader className="px-0 pt-0">
            <CardTitle className="text-lg">Profile</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 px-0 pb-0">
            <div className="flex justify-between gap-4">
              <span className="text-sm text-muted-foreground">Name</span>
              <span className="text-sm font-medium text-right">{teacher.name}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-sm text-muted-foreground">Phone</span>
              <span className="text-sm font-medium text-right">{teacher.phone}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-sm text-muted-foreground">Email</span>
              <span className="text-sm font-medium text-right break-all">{teacher.email || "—"}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-sm text-muted-foreground">Role</span>
              <span className="text-sm font-medium text-right">{teacher.role}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-sm text-muted-foreground">Joined</span>
              <span className="text-sm font-medium text-right">
                {formatDate(teacher.createdAt)}
              </span>
            </div>
          </CardContent>
        </PageSection>

        <PageSection>
          <CardHeader className="px-0 pt-0">
            <CardTitle className="text-lg">Assignments</CardTitle>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            {!isTeacherAccount && (
              <StatusBanner variant="info">
                Admin accounts do not have class or subject assignments.
              </StatusBanner>
            )}
            {isTeacherAccount && showAssignForm && (
              <form
                onSubmit={handleAssign}
                className="mb-4 space-y-3 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_16px_40px_-30px_rgba(15,40,69,0.42)] dark:bg-card/88"
              >
                <ClassSelector
                  classes={classes}
                  value={assignClassId}
                  onChange={setAssignClassId}
                  disabled={isAssigning}
                  label="Class"
                />
                <Select
                  id="assignSubject"
                  label="Subject"
                  value={assignSubject}
                  onChange={(e) => setAssignSubject(e.target.value)}
                  disabled={isAssigning}
                >
                  <option value="" disabled>
                    Select a subject
                  </option>
                  {subjects.map((s) => (
                    <option key={s.id} value={s.name}>
                      {s.name}
                    </option>
                  ))}
                </Select>
                <label className="flex items-center gap-3 rounded-[20px] border border-border/70 bg-card/74 px-4 py-3 text-sm text-foreground shadow-[0_14px_32px_-26px_rgba(15,40,69,0.4)] dark:bg-card/90">
                  <input
                    type="checkbox"
                    checked={assignIsClassTeacher}
                    onChange={(e) => setAssignIsClassTeacher(e.target.checked)}
                    disabled={isAssigning}
                    className="h-4 w-4 rounded border-border text-primary focus:ring-primary"
                  />
                  Mark this assignment as the class teacher for this class
                </label>
                {(classes.length === 0 || subjects.length === 0) && (
                  <StatusBanner variant="warning">
                    {classes.length === 0
                      ? "Create a class before assigning this teacher."
                      : "Create a subject before assigning this teacher."}
                  </StatusBanner>
                )}
                {assignError && (
                  <StatusBanner variant="error">{assignError}</StatusBanner>
                )}
                <div className="flex gap-2">
                  <Button
                    type="submit"
                    size="sm"
                    disabled={isAssigning || classes.length === 0 || subjects.length === 0}
                  >
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
                {isTeacherAccount
                  ? "No class assignments yet."
                  : "No assignments apply to admin accounts."}
              </p>
            ) : (
              <ul className="space-y-3" aria-label="Teacher assignments">
                {teacher.assignments.map((a) => (
                  <li
                    key={a.assignmentId}
                    className="flex items-center justify-between gap-4 rounded-[24px] border border-border/70 bg-card/80 p-4 shadow-[0_18px_46px_-34px_rgba(15,40,69,0.42)] dark:bg-card/90"
                  >
                    <div>
                      <p className="font-medium">
                        {a.className}
                        {a.section ? ` ${a.section}` : ""}
                      </p>
                      <div className="mt-1 flex flex-wrap items-center gap-2">
                        <p className="text-sm text-muted-foreground">
                          {a.subject}
                        </p>
                        {a.isClassTeacher && (
                          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-semibold text-primary">
                            Class teacher
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {!a.isClassTeacher && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            void handlePromoteClassTeacher(a.assignmentId);
                          }}
                          disabled={isPromoting === a.assignmentId}
                        >
                          {isPromoting === a.assignmentId ? (
                            <Spinner size="sm" />
                          ) : (
                            "Make Class Teacher"
                          )}
                        </Button>
                      )}
                      {/* Labelled "Unassign" instead of a trash icon so the
                          action reads as "remove this teacher↔class link",
                          not "delete a record". Mirrors the pattern used on
                          the class-detail page. */}
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() =>
                          handleRemove(
                            a.assignmentId,
                            `${a.className} ${a.section} — ${a.subject}`
                          )
                        }
                        disabled={isRemoving === a.assignmentId}
                        aria-label={`Unassign ${a.subject} from ${a.className}`}
                        className="text-destructive hover:text-destructive"
                      >
                        {isRemoving === a.assignmentId ? (
                          <Spinner size="sm" />
                        ) : (
                          <>
                            <UserMinus className="h-4 w-4" aria-hidden="true" />
                            Unassign
                          </>
                        )}
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </PageSection>
      </div>
    </PageShell>
  );
}
