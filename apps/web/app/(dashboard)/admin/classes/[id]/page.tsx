"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiDelete, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft, GraduationCap, Trash2, UserPlus } from "lucide-react";
import type { ClassItem, StudentListItem, MutationResponse, PagedResult } from "@/lib/types/student";
import type {
  AssignClassRequest,
  SubjectItem,
  TeacherMutationResponse,
  TeacherPagedResult,
} from "@/lib/types/teacher";
import type { ClassAssignmentItem } from "@/lib/types/class-assignments";

export default function AdminClassDetailPage(): React.ReactElement {
  const params = useParams();
  const router = useRouter();
  const classId = params.id as string;

  const [classItem, setClassItem] = React.useState<ClassItem | null>(null);
  const [assignments, setAssignments] = React.useState<ClassAssignmentItem[]>([]);
  const [students, setStudents] = React.useState<StudentListItem[]>([]);
  const [subjects, setSubjects] = React.useState<SubjectItem[]>([]);

  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  // Assign form
  const [showAssignForm, setShowAssignForm] = React.useState(false);
  const [teacherSearch, setTeacherSearch] = React.useState("");
  const [teachers, setTeachers] = React.useState<TeacherPagedResult | null>(null);
  const [selectedTeacherId, setSelectedTeacherId] = React.useState("");
  const [selectedSubject, setSelectedSubject] = React.useState("");
  const [markAsClassTeacher, setMarkAsClassTeacher] = React.useState(false);
  const [isAssigning, setIsAssigning] = React.useState(false);
  const [assignError, setAssignError] = React.useState("");

  const [actionAssignmentId, setActionAssignmentId] = React.useState<string | null>(null);
  const assignFormRef = React.useRef<HTMLDivElement | null>(null);

  const getBestErrorMessage = React.useCallback((err: unknown, fallback: string): string => {
    if (!(err instanceof ApiError)) return fallback;
    const firstValidationMessage = err.details?.errors
      ? Object.values(err.details.errors).flat()[0]
      : undefined;
    return firstValidationMessage || err.message || fallback;
  }, []);

  const classTeacher = React.useMemo(
    () => assignments.find((a) => a.isClassTeacher) ?? null,
    [assignments]
  );

  const groupedBySubject = React.useMemo(() => {
    const map = new Map<string, ClassAssignmentItem[]>();
    for (const a of assignments) {
      const list = map.get(a.subject) ?? [];
      list.push(a);
      map.set(a.subject, list);
    }
    return Array.from(map.entries())
      .map(([subject, items]) => ({
        subject,
        items: items.sort((x, y) => x.teacherName.localeCompare(y.teacherName)),
      }))
      .sort((a, b) => a.subject.localeCompare(b.subject));
  }, [assignments]);

  const fetchTeachers = React.useCallback(async (): Promise<void> => {
    try {
      const params = new URLSearchParams();
      if (teacherSearch.trim()) params.set("search", teacherSearch.trim());
      params.set("page", "1");
      params.set("pageSize", "50");
      const data = await apiGet<TeacherPagedResult>(`${API_ENDPOINTS.teachers}?${params.toString()}`);
      setTeachers(data);
    } catch (err) {
      // Non-fatal for the page; surface in assign form when open.
      setAssignError(getBestErrorMessage(err, "Failed to load teachers."));
    }
  }, [getBestErrorMessage, teacherSearch]);

  const fetchAll = React.useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError("");
    setSuccessMessage("");

    try {
      const [classesData, assignmentData, studentData, subjectData] = await Promise.all([
        apiGet<ClassItem[]>(API_ENDPOINTS.classes),
        apiGet<ClassAssignmentItem[]>(`${API_ENDPOINTS.classes}/${classId}/assignments`),
        apiGet<PagedResult<StudentListItem>>(`${API_ENDPOINTS.students}?classId=${classId}&pageSize=100`),
        apiGet<SubjectItem[]>(API_ENDPOINTS.subjects),
      ]);

      const found = classesData.find((c) => c.id === classId) ?? null;
      setClassItem(found);
      setAssignments(assignmentData);
      setStudents(studentData.items ?? []);
      setSubjects(subjectData);
    } catch (err) {
      setError(getBestErrorMessage(err, "Failed to load class details."));
    } finally {
      setIsLoading(false);
    }
  }, [classId, getBestErrorMessage]);

  React.useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  React.useEffect(() => {
    if (!showAssignForm) return;
    void fetchTeachers();
  }, [fetchTeachers, showAssignForm]);

  React.useEffect(() => {
    if (!showAssignForm) return;
    assignFormRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [showAssignForm]);

  // Debounce teacher search when assign form is open
  React.useEffect(() => {
    if (!showAssignForm) return;
    const t = setTimeout(() => {
      void fetchTeachers();
    }, 250);
    return () => clearTimeout(t);
  }, [fetchTeachers, showAssignForm, teacherSearch]);

  const handleAssign = async (e: React.FormEvent<HTMLFormElement>): Promise<void> => {
    e.preventDefault();
    setAssignError("");
    setSuccessMessage("");

    if (!selectedTeacherId || !selectedSubject) {
      setAssignError("Please select both a teacher and a subject.");
      return;
    }

    if (markAsClassTeacher && classTeacher) {
      setAssignError("This class already has a class teacher assigned. Unassign the current class teacher first.");
      return;
    }

    setIsAssigning(true);
    try {
      const body: AssignClassRequest = {
        classId,
        subject: selectedSubject,
        isClassTeacher: markAsClassTeacher,
      };

      const res = await apiPost<TeacherMutationResponse>(
        `${API_ENDPOINTS.teachers}/${selectedTeacherId}/assignments`,
        body
      );
      setSuccessMessage(res.message);
      setShowAssignForm(false);
      setTeacherSearch("");
      setSelectedTeacherId("");
      setSelectedSubject("");
      setMarkAsClassTeacher(false);
      await fetchAll();
    } catch (err) {
      setAssignError(getBestErrorMessage(err, "Failed to assign teacher."));
    } finally {
      setIsAssigning(false);
    }
  };

  const handlePromote = async (assignment: ClassAssignmentItem): Promise<void> => {
    setActionAssignmentId(assignment.assignmentId);
    setError("");
    setSuccessMessage("");

    if (classTeacher) {
      setError("This class already has a class teacher assigned. Unassign the current class teacher first.");
      setActionAssignmentId(null);
      return;
    }

    try {
      const res = await apiPut<MutationResponse>(
        `${API_ENDPOINTS.teachers}/${assignment.teacherId}/assignments/${assignment.assignmentId}/class-teacher`
      );
      setSuccessMessage(res.message);
      await fetchAll();
    } catch (err) {
      setError(getBestErrorMessage(err, "Failed to update class teacher."));
    } finally {
      setActionAssignmentId(null);
    }
  };

  const handleUnassign = async (assignment: ClassAssignmentItem): Promise<void> => {
    if (
      !confirm(`Unassign ${assignment.teacherName} from ${assignment.subject}?`)
    ) {
      return;
    }

    setActionAssignmentId(assignment.assignmentId);
    setError("");
    setSuccessMessage("");

    try {
      const res = await apiDelete<MutationResponse>(
        `${API_ENDPOINTS.teachers}/${assignment.teacherId}/assignments/${assignment.assignmentId}`
      );
      setSuccessMessage(res.message || "Assignment removed.");
      await fetchAll();
    } catch (err) {
      setError(getBestErrorMessage(err, "Failed to remove assignment."));
    } finally {
      setActionAssignmentId(null);
    }
  };

  const title = classItem
    ? `${classItem.name}${classItem.section ? ` ${classItem.section}` : ""}`
    : "Class";

  if (isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  if (error && !classItem) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState title="Error" message={error} onRetry={fetchAll} />
      </div>
    );
  }

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title={title}
        description={classItem ? `Academic year ${classItem.academicYear || "—"}` : "Class detail"}
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push("/admin/classes")}
            aria-label="Back to classes"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Classes
          </Button>
        )}
        actions={(
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setShowAssignForm((p) => !p);
              setAssignError("");
            }}
          >
            <UserPlus className="h-4 w-4" />
            Assign teacher
          </Button>
        )}
        stats={[
          { label: "Students", value: (classItem?.studentCount ?? students.length).toString() },
          { label: "Assignments", value: assignments.length.toString() },
        ]}
      />

      {successMessage ? <StatusBanner variant="success">{successMessage}</StatusBanner> : null}
      {error ? <StatusBanner variant="error">{error}</StatusBanner> : null}

      <div className="grid gap-4 xl:grid-cols-2">
        <PageSection className="space-y-4">
          {showAssignForm && (
            <Card ref={assignFormRef}>
              <CardHeader className="pb-2">
                <CardTitle className="text-lg">Assign teacher</CardTitle>
              </CardHeader>
              <CardContent>
                <form onSubmit={handleAssign} className="space-y-3">
                  <Input
                    label="Search teacher"
                    placeholder="Type name or phone…"
                    value={teacherSearch}
                    onChange={(e) => setTeacherSearch(e.target.value)}
                    disabled={isAssigning}
                  />
                  <Select
                    label="Teacher"
                    value={selectedTeacherId}
                    onChange={(e) => setSelectedTeacherId(e.target.value)}
                    disabled={isAssigning}
                  >
                    <option value="" disabled>
                      {teachers ? "Select a teacher" : "Loading teachers..."}
                    </option>
                    {(teachers?.items ?? []).map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.name} ({t.phone})
                      </option>
                    ))}
                  </Select>
                  <Select
                    label="Subject"
                    value={selectedSubject}
                    onChange={(e) => setSelectedSubject(e.target.value)}
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
                  <label className="flex items-center gap-3 rounded-[20px] border border-border/70 bg-card/74 px-4 py-3 text-sm text-foreground shadow-[0_14px_32px_-26px_rgba(15,23,42,0.4)] dark:bg-card/90">
                    <input
                      type="checkbox"
                      checked={markAsClassTeacher}
                      onChange={(e) => setMarkAsClassTeacher(e.target.checked)}
                      disabled={isAssigning || !!classTeacher}
                      className="h-4 w-4 rounded border-border text-primary focus:ring-primary"
                    />
                    Mark this assignment as the class teacher
                    {classTeacher ? (
                      <span className="ml-auto text-xs text-muted-foreground">
                        (already assigned)
                      </span>
                    ) : null}
                  </label>
                  {assignError ? <StatusBanner variant="error">{assignError}</StatusBanner> : null}
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
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-lg">Class teacher</CardTitle>
            </CardHeader>
            <CardContent>
              {!classTeacher ? (
                <EmptyState
                  title="No class teacher"
                  description="Promote an assignment to class teacher, or assign a teacher and mark as class teacher."
                />
              ) : (
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className="truncate font-semibold">{classTeacher.teacherName}</p>
                    <p className="text-sm text-muted-foreground">{classTeacher.teacherPhone}</p>
                    <div className="mt-2 flex flex-wrap items-center gap-2">
                      <Badge variant="secondary">{classTeacher.subject}</Badge>
                      <Badge variant="default">Class teacher</Badge>
                    </div>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => void handleUnassign(classTeacher)}
                    disabled={actionAssignmentId === classTeacher.assignmentId}
                    aria-label="Unassign class teacher"
                    className="h-9 w-9 text-destructive hover:text-destructive"
                  >
                    {actionAssignmentId === classTeacher.assignmentId ? (
                      <Spinner size="sm" />
                    ) : (
                      <Trash2 className="h-4 w-4" />
                    )}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-lg">Teacher assignments (by subject)</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {assignments.length === 0 ? (
                <EmptyState
                  title="No assignments"
                  description="Assign teachers to start managing subject coverage for this class."
                />
              ) : (
                groupedBySubject.map((group) => (
                  <div key={group.subject} className="space-y-2">
                    <div className="flex items-center justify-between gap-2">
                      <div className="text-sm font-semibold text-foreground">
                        {group.subject}
                      </div>
                      <Badge variant="outline">{group.items.length}</Badge>
                    </div>
                    <div className="space-y-2">
                      {group.items.map((a) => (
                        <div
                          key={a.assignmentId}
                          className="flex flex-col gap-2 rounded-[20px] border border-border/70 bg-card/72 p-3 shadow-[0_14px_32px_-26px_rgba(15,23,42,0.38)] sm:flex-row sm:items-center sm:justify-between"
                        >
                          <div className="min-w-0">
                            <p className="truncate text-sm font-semibold">{a.teacherName}</p>
                            <p className="text-xs text-muted-foreground">{a.teacherPhone}</p>
                            {a.isClassTeacher ? (
                              <div className="mt-2">
                                <Badge variant="default">Class teacher</Badge>
                              </div>
                            ) : null}
                          </div>
                          <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                            {!a.isClassTeacher ? (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => void handlePromote(a)}
                                disabled={!!classTeacher || actionAssignmentId === a.assignmentId}
                              >
                                {actionAssignmentId === a.assignmentId ? (
                                  <Spinner size="sm" />
                                ) : (
                                  "Make Class Teacher"
                                )}
                              </Button>
                            ) : null}
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => void handleUnassign(a)}
                              disabled={actionAssignmentId === a.assignmentId}
                              aria-label="Unassign"
                              className="h-9 w-9 text-destructive hover:text-destructive"
                            >
                              {actionAssignmentId === a.assignmentId ? (
                                <Spinner size="sm" />
                              ) : (
                                <Trash2 className="h-4 w-4" />
                              )}
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </PageSection>

        <PageSection className="space-y-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-lg">Students</CardTitle>
            </CardHeader>
            <CardContent>
              {students.length === 0 ? (
                <EmptyState
                  title="No students"
                  description="No active students found for this class."
                />
              ) : (
                <div className="space-y-2">
                  {students.map((s) => (
                    <Link
                      key={s.id}
                      href={`/admin/students/${s.id}`}
                      className="focus-ring flex items-center justify-between gap-3 rounded-[20px] border border-border/70 bg-card/72 p-3 text-sm shadow-[0_14px_32px_-26px_rgba(15,23,42,0.38)] transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card/92"
                    >
                      <div className="min-w-0">
                        <div className="truncate font-semibold text-foreground">{s.name}</div>
                        <div className="text-xs text-muted-foreground">
                          Roll {s.rollNumber}
                        </div>
                      </div>
                      {!s.isActive ? <Badge variant="destructive">Inactive</Badge> : null}
                    </Link>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </PageSection>
      </div>
    </PageShell>
  );
}

