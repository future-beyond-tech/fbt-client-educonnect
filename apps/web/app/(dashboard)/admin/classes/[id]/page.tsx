"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiDelete, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft, GraduationCap, Pencil, Trash2, UserPlus } from "lucide-react";
import type {
  ClassItem,
  ClassMutationResponse,
  StudentListItem,
  MutationResponse,
  PagedResult,
  UpdateClassRequest,
} from "@/lib/types/student";
import type {
  AssignClassRequest,
  SubjectItem,
  TeacherMutationResponse,
  TeacherPagedResult,
} from "@/lib/types/teacher";
import type { ClassAssignmentItem } from "@/lib/types/class-assignments";

// Shape of a pending class-teacher replacement awaiting admin confirmation.
// Carries enough context to (a) show the right dialog copy and (b) fire the
// correct API call after Confirm — the two paths are a fresh Assign (POST)
// and a Promote of an existing subject assignment (PUT).
type ReplaceClassTeacherPayload =
  | {
      kind: "assign";
      teacherId: string;
      teacherName: string;
      subject: string;
    }
  | {
      kind: "promote";
      assignment: ClassAssignmentItem;
    };

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

  // Edit-class dialog state
  const [editDialogOpen, setEditDialogOpen] = React.useState(false);
  const [editName, setEditName] = React.useState("");
  const [editSection, setEditSection] = React.useState("");
  const [editAcademicYear, setEditAcademicYear] = React.useState("");
  const [editError, setEditError] = React.useState("");
  const [isSavingClass, setIsSavingClass] = React.useState(false);

  // Replace-class-teacher confirmation state. When set, the dialog at the
  // bottom of the page opens — the admin must explicitly click Replace
  // before we demote the existing class teacher. Null = dialog closed.
  const [replacePayload, setReplacePayload] =
    React.useState<ReplaceClassTeacherPayload | null>(null);
  const [isReplacing, setIsReplacing] = React.useState(false);

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

    // If the admin is assigning a new class teacher while one already exists,
    // surface the confirmation dialog instead of failing silently. The
    // backend supports atomic replacement (demotes the old class teacher to
    // subject teacher in the same request), but we never want that to happen
    // without explicit admin consent.
    if (markAsClassTeacher && classTeacher) {
      const incomingTeacher = teachers?.items.find((t) => t.id === selectedTeacherId);
      setReplacePayload({
        kind: "assign",
        teacherId: selectedTeacherId,
        teacherName: incomingTeacher?.name ?? "the selected teacher",
        subject: selectedSubject,
      });
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

    // When a class teacher already exists, route through the confirmation
    // dialog instead of blocking. The dialog's Replace action will call the
    // promote endpoint, which demotes the existing class teacher atomically.
    if (classTeacher && classTeacher.assignmentId !== assignment.assignmentId) {
      setReplacePayload({ kind: "promote", assignment });
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

  const confirmReplaceClassTeacher = async (): Promise<void> => {
    if (!replacePayload) return;
    setIsReplacing(true);
    setError("");
    setAssignError("");
    setSuccessMessage("");

    try {
      if (replacePayload.kind === "assign") {
        const body: AssignClassRequest = {
          classId,
          subject: replacePayload.subject,
          isClassTeacher: true,
        };
        const res = await apiPost<TeacherMutationResponse>(
          `${API_ENDPOINTS.teachers}/${replacePayload.teacherId}/assignments`,
          body
        );
        setSuccessMessage(res.message);
        // Reset the assign form so the admin lands on a clean slate.
        setShowAssignForm(false);
        setTeacherSearch("");
        setSelectedTeacherId("");
        setSelectedSubject("");
        setMarkAsClassTeacher(false);
      } else {
        const a = replacePayload.assignment;
        const res = await apiPut<MutationResponse>(
          `${API_ENDPOINTS.teachers}/${a.teacherId}/assignments/${a.assignmentId}/class-teacher`
        );
        setSuccessMessage(res.message);
      }
      setReplacePayload(null);
      await fetchAll();
    } catch (err) {
      const msg = getBestErrorMessage(err, "Failed to replace class teacher.");
      // Route the error to whichever banner is visually closest to the
      // control the admin started from.
      if (replacePayload.kind === "assign") {
        setAssignError(msg);
      } else {
        setError(msg);
      }
    } finally {
      setIsReplacing(false);
    }
  };

  const openEditDialog = (): void => {
    if (!classItem) return;
    setSuccessMessage("");
    setEditError("");
    setEditName(classItem.name);
    setEditSection(classItem.section);
    setEditAcademicYear(classItem.academicYear);
    setEditDialogOpen(true);
  };

  const closeEditDialog = (): void => {
    setEditDialogOpen(false);
    setEditError("");
  };

  const handleUpdateClass = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setEditError("");
    setSuccessMessage("");

    const trimmedName = editName.trim();
    const trimmedSection = editSection.trim();
    const trimmedYear = editAcademicYear.trim();

    if (!trimmedName || !trimmedSection || !trimmedYear) {
      setEditError("Class name, section, and academic year are required.");
      return;
    }

    setIsSavingClass(true);
    try {
      const body: UpdateClassRequest = {
        name: trimmedName,
        section: trimmedSection,
        academicYear: trimmedYear,
      };
      const result = await apiPut<ClassMutationResponse>(
        `${API_ENDPOINTS.classes}/${classId}`,
        body
      );
      setSuccessMessage(result.message);
      setEditDialogOpen(false);
      await fetchAll();
    } catch (err) {
      setEditError(getBestErrorMessage(err, "Failed to update class."));
    } finally {
      setIsSavingClass(false);
    }
  };

  const handleUnassign = async (assignment: ClassAssignmentItem): Promise<void> => {
    const confirmMessage = assignment.isClassTeacher
      ? `Remove class-teacher role from ${assignment.teacherName}? They will remain assigned to this class as the ${assignment.subject} teacher.`
      : `Unassign ${assignment.teacherName} from ${assignment.subject}?`;

    if (!confirm(confirmMessage)) {
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
          <div className="flex flex-wrap items-center gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={openEditDialog}
              disabled={!classItem}
              aria-label="Edit class details"
            >
              <Pencil className="h-4 w-4" />
              Edit details
            </Button>
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
          </div>
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
                  <label className="flex items-center gap-3 rounded-[20px] border border-border/70 bg-card/74 px-4 py-3 text-sm text-foreground shadow-[0_14px_32px_-26px_rgba(15,40,69,0.4)] dark:bg-card/90">
                    <input
                      type="checkbox"
                      checked={markAsClassTeacher}
                      onChange={(e) => setMarkAsClassTeacher(e.target.checked)}
                      // Only disable during the request itself — if this class
                      // already has a class teacher, the checkbox remains
                      // interactive and the admin will be prompted to
                      // confirm replacement on submit.
                      disabled={isAssigning}
                      className="h-4 w-4 rounded border-border text-primary focus:ring-primary"
                    />
                    Mark this assignment as the class teacher
                    {classTeacher && markAsClassTeacher ? (
                      <span className="ml-auto text-xs text-muted-foreground">
                        will replace {classTeacher.teacherName}
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
                          className="flex flex-col gap-2 rounded-[20px] border border-border/70 bg-card/72 p-3 shadow-[0_14px_32px_-26px_rgba(15,40,69,0.38)] sm:flex-row sm:items-center sm:justify-between"
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
                                // Stay enabled even when a class teacher
                                // already exists — clicking will surface the
                                // replacement-confirmation dialog rather than
                                // silently failing.
                                disabled={actionAssignmentId === a.assignmentId}
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
                      className="focus-ring flex items-center justify-between gap-3 rounded-[20px] border border-border/70 bg-card/72 p-3 text-sm shadow-[0_14px_32px_-26px_rgba(15,40,69,0.38)] transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card/92"
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

      <Dialog
        open={editDialogOpen}
        onOpenChange={(next) => {
          if (!next) closeEditDialog();
          else setEditDialogOpen(true);
        }}
        title="Edit class"
        description="Update the class name, section, or academic year."
        footer={
          <>
            <Button
              type="button"
              variant="outline"
              onClick={closeEditDialog}
              disabled={isSavingClass}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              form="class-detail-edit-form"
              disabled={isSavingClass}
            >
              {isSavingClass ? <Spinner size="sm" /> : "Save changes"}
            </Button>
          </>
        }
      >
        <form id="class-detail-edit-form" onSubmit={handleUpdateClass} className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <Input
              label="Class name"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              placeholder="e.g. 5"
              disabled={isSavingClass}
              data-autofocus
            />
            <Input
              label="Section"
              value={editSection}
              onChange={(e) => setEditSection(e.target.value)}
              placeholder="e.g. A"
              disabled={isSavingClass}
            />
            <Input
              label="Academic year"
              value={editAcademicYear}
              onChange={(e) => setEditAcademicYear(e.target.value)}
              placeholder="e.g. 2026-27"
              disabled={isSavingClass}
            />
          </div>
          {editError && <StatusBanner variant="error">{editError}</StatusBanner>}
        </form>
      </Dialog>

      <Dialog
        open={!!replacePayload}
        onOpenChange={(next) => {
          // Don't let a click-outside / ESC close the dialog mid-request — we
          // don't know the server state until the in-flight replace resolves.
          if (isReplacing) return;
          if (!next) setReplacePayload(null);
        }}
        title="Replace class teacher?"
        description="This class already has a class teacher assigned."
        disableBackdropClose={isReplacing}
        disableEscClose={isReplacing}
        footer={
          <>
            <Button
              type="button"
              variant="outline"
              onClick={() => setReplacePayload(null)}
              disabled={isReplacing}
            >
              Cancel
            </Button>
            <Button
              type="button"
              variant="destructive"
              onClick={() => void confirmReplaceClassTeacher()}
              disabled={isReplacing}
            >
              {isReplacing ? <Spinner size="sm" /> : "Replace"}
            </Button>
          </>
        }
      >
        {replacePayload && classTeacher ? (
          <div className="space-y-3 text-sm">
            <div className="rounded-[16px] border border-border/70 bg-card/72 p-3">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">
                Current class teacher
              </p>
              <p className="mt-1 font-semibold">{classTeacher.teacherName}</p>
              <p className="text-xs text-muted-foreground">
                {classTeacher.subject}
              </p>
            </div>
            <div className="rounded-[16px] border border-border/70 bg-card/72 p-3">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">
                New class teacher
              </p>
              <p className="mt-1 font-semibold">
                {replacePayload.kind === "assign"
                  ? replacePayload.teacherName
                  : replacePayload.assignment.teacherName}
              </p>
              <p className="text-xs text-muted-foreground">
                {replacePayload.kind === "assign"
                  ? replacePayload.subject
                  : replacePayload.assignment.subject}
              </p>
            </div>
            <p className="text-muted-foreground">
              {classTeacher.teacherName} will remain assigned to this class as
              the {classTeacher.subject} teacher but will lose the class
              teacher role. This does not remove any attendance or homework
              records.
            </p>
          </div>
        ) : null}
      </Dialog>
    </PageShell>
  );
}

