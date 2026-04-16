"use client";

import * as React from "react";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
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
import { CalendarDays, CheckCircle2, Clock, Pencil, XCircle } from "lucide-react";
import type { TeacherClassItem } from "@/lib/types/teacher";

type TakeStatus = "Present" | "Absent" | "Late";

interface TakeStudent {
  id: string;
  name: string;
  rollNumber: string;
}

interface TakeException {
  studentId: string;
  status: "Absent" | "Late";
  reason: string | null;
}

interface TakeLeave {
  leaveId: string;
  studentId: string;
  studentName: string;
  rollNumber: string;
  startDate: string;
  endDate: string;
  reason: string;
  status: "Pending" | "Approved";
}

interface TakeContextResponse {
  classId: string;
  date: string;
  students: TakeStudent[];
  exceptions: TakeException[];
  approvedLeaves: TakeLeave[];
  pendingLeaves: TakeLeave[];
}

interface SubmitAttendanceResponse {
  createdCount: number;
  updatedCount: number;
  clearedCount: number;
  message: string;
}

export default function TeacherAttendancePage(): React.ReactElement {
  const { token, isLoading: isAuthLoading } = useAuth();
  const today = React.useMemo(() => new Date().toISOString().slice(0, 10), []);

  // Assignments => classes dropdown
  const [assignments, setAssignments] = React.useState<TeacherClassItem[]>([]);
  const [isLoadingAssignments, setIsLoadingAssignments] = React.useState(true);
  const [assignmentError, setAssignmentError] = React.useState("");

  // Take attendance state
  const [classId, setClassId] = React.useState("");
  const [date, setDate] = React.useState(today);
  const [context, setContext] = React.useState<TakeContextResponse | null>(null);
  const [isLoadingContext, setIsLoadingContext] = React.useState(false);
  const [contextError, setContextError] = React.useState("");

  const [statusByStudentId, setStatusByStudentId] = React.useState<Record<string, TakeStatus>>({});
  const [reasonByStudentId, setReasonByStudentId] = React.useState<Record<string, string>>({});

  // Tracks whether attendance has been saved (or was already persisted) for
  // the current class+date. When true, rows are locked and show an Edit button.
  const [hasSaved, setHasSaved] = React.useState(false);
  // Students the user has explicitly re-opened for editing after a save.
  const [editingStudentIds, setEditingStudentIds] = React.useState<Set<string>>(new Set());

  const [actionError, setActionError] = React.useState("");
  const [actionSuccess, setActionSuccess] = React.useState("");
  const [isSaving, setIsSaving] = React.useState(false);
  const [leaveActionId, setLeaveActionId] = React.useState<string | null>(null);

  // The /api/attendance/take endpoint requires IsClassTeacher=true on the
  // teacher's assignment for the selected class — subject-teacher assignments
  // get a 403 "Only the class teacher can take attendance for this class."
  // Keep this list in lockstep with the backend rule so we never offer (or
  // auto-select) a class the server will reject.
  const classesForTeacher = React.useMemo(() => {
    const map = new Map<string, { classId: string; className: string; section: string; isClassTeacher: boolean }>();
    for (const a of assignments) {
      if (!a.isClassTeacher) continue;
      const existing = map.get(a.classId);
      if (existing) {
        existing.isClassTeacher = true;
      } else {
        map.set(a.classId, {
          classId: a.classId,
          className: a.className,
          section: a.section,
          isClassTeacher: true,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) =>
      (a.className + a.section).localeCompare(b.className + b.section)
    );
  }, [assignments]);

  const hasAnyAssignments = assignments.length > 0;
  const hasClassTeacherAssignment = classesForTeacher.length > 0;

  const fetchAssignments = React.useCallback(async () => {
    setIsLoadingAssignments(true);
    setAssignmentError("");
    try {
      const data = await apiGet<TeacherClassItem[]>(API_ENDPOINTS.teachersMyClasses);
      setAssignments(data);
    } catch (err) {
      setAssignmentError(err instanceof ApiError ? err.message : "Failed to load assignments.");
    } finally {
      setIsLoadingAssignments(false);
    }
  }, []);

  const hydrateFormFromContext = React.useCallback((ctx: TakeContextResponse) => {
    const nextStatus: Record<string, TakeStatus> = {};
    const nextReason: Record<string, string> = {};

    for (const s of ctx.students) {
      nextStatus[s.id] = "Present";
      nextReason[s.id] = "";
    }

    for (const ex of ctx.exceptions) {
      nextStatus[ex.studentId] = ex.status === "Late" ? "Late" : "Absent";
      nextReason[ex.studentId] = ex.reason ?? "";
    }

    // Approved leave: lock to Present (excused)
    for (const leave of ctx.approvedLeaves) {
      nextStatus[leave.studentId] = "Present";
      nextReason[leave.studentId] = "";
    }

    setStatusByStudentId(nextStatus);
    setReasonByStudentId(nextReason);
    // If the backend already has exception records for this class+date, the
    // teacher has previously saved attendance — lock the rows and surface an
    // Edit affordance. Fresh (never-saved) contexts stay fully editable.
    setHasSaved(ctx.exceptions.length > 0);
    setEditingStudentIds(new Set());
  }, []);

  const fetchContext = React.useCallback(async () => {
    if (!classId || !date) return;
    setIsLoadingContext(true);
    setContextError("");
    setActionError("");
    setActionSuccess("");

    try {
      const data = await apiGet<TakeContextResponse>(
        `${API_ENDPOINTS.attendance}/take?classId=${classId}&date=${date}`
      );
      setContext(data);
      hydrateFormFromContext(data);
    } catch (err) {
      setContext(null);
      setContextError(err instanceof ApiError ? err.message : "Failed to load attendance.");
    } finally {
      setIsLoadingContext(false);
    }
  }, [classId, date, hydrateFormFromContext]);

  React.useEffect(() => {
    if (isAuthLoading) return;
    if (!token) return;
    fetchAssignments();
  }, [fetchAssignments, isAuthLoading, token]);

  React.useEffect(() => {
    if (classId && date) {
      void fetchContext();
    }
  }, [classId, date, fetchContext]);

  React.useEffect(() => {
    // Default to first class-teacher assignment
    if (classId) return;
    if (classesForTeacher.length === 0) return;
    setClassId(classesForTeacher[0]?.classId ?? "");
  }, [classId, classesForTeacher]);

  const isStudentOnApprovedLeave = React.useCallback(
    (studentId: string): boolean => !!context?.approvedLeaves.some((l) => l.studentId === studentId),
    [context?.approvedLeaves]
  );

  const saveAttendance = async (): Promise<void> => {
    if (!context) return;
    setIsSaving(true);
    setActionError("");
    setActionSuccess("");

    try {
      const items = context.students.map((s) => ({
        studentId: s.id,
        status: statusByStudentId[s.id] ?? "Present",
        reason: (reasonByStudentId[s.id] ?? "").trim() || null,
      }));

      const res = await apiPost<SubmitAttendanceResponse>(`${API_ENDPOINTS.attendance}/take`, {
        classId: context.classId,
        date: context.date,
        items,
      });

      // Lock every row and surface the Edit affordance so the teacher has a
      // clear visual confirmation the save took effect.
      setHasSaved(true);
      setEditingStudentIds(new Set());
      setActionSuccess(
        res.message ||
          `Attendance saved successfully (${res.createdCount} created, ${res.updatedCount} updated).`
      );

      // Scroll the success banner into view — on mobile the teacher is often
      // scrolled down to the last student when they hit Save, and the banner
      // is rendered near the top of the page.
      if (typeof window !== "undefined") {
        window.scrollTo({ top: 0, behavior: "smooth" });
      }

      // Intentionally do NOT refetch here: fetchContext() clears
      // actionSuccess, which would make the banner flash and disappear.
      // Local state (hasSaved, statusByStudentId, reasonByStudentId) already
      // reflects the write, and nothing returned by the /take GET can change
      // as a side-effect of saving. The next class/date change will refetch.
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to save attendance.");
    } finally {
      setIsSaving(false);
    }
  };

  const unlockStudentForEdit = (studentId: string): void => {
    setEditingStudentIds((prev) => {
      const next = new Set(prev);
      next.add(studentId);
      return next;
    });
    setActionSuccess("");
  };

  const isRowLocked = (studentId: string): boolean => {
    if (!hasSaved) return false;
    return !editingStudentIds.has(studentId);
  };

  const approveLeave = async (leaveId: string): Promise<void> => {
    setLeaveActionId(leaveId);
    setActionError("");
    setActionSuccess("");
    try {
      const res = await apiPut<{ message: string }>(
        `${API_ENDPOINTS.leaveApplications}/${leaveId}/approve`,
        {}
      );
      setActionSuccess(res.message);
      await fetchContext();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to approve leave.");
    } finally {
      setLeaveActionId(null);
    }
  };

  const rejectLeave = async (leaveId: string): Promise<void> => {
    const note = window.prompt("Rejection note");
    if (!note) return;

    setLeaveActionId(leaveId);
    setActionError("");
    setActionSuccess("");
    try {
      const res = await apiPut<{ message: string }>(
        `${API_ENDPOINTS.leaveApplications}/${leaveId}/reject`,
        { reviewNote: note }
      );
      setActionSuccess(res.message);
      await fetchContext();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to reject leave.");
    } finally {
      setLeaveActionId(null);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title="Attendance"
        description="Take class attendance quickly and review leave requests."
        icon={<CalendarDays className="h-6 w-6" aria-hidden="true" />}
        stats={[
          { label: "Classes", value: classesForTeacher.length.toString() },
          { label: "Date", value: date },
        ]}
      />

      {assignmentError ? <StatusBanner variant="error">{assignmentError}</StatusBanner> : null}
      {actionError ? <StatusBanner variant="error">{actionError}</StatusBanner> : null}
      {actionSuccess ? <StatusBanner variant="success">{actionSuccess}</StatusBanner> : null}
      {!isLoadingAssignments && hasAnyAssignments && !hasClassTeacherAssignment ? (
        <StatusBanner variant="info" title="You&apos;re not the class teacher for any class">
          Attendance can only be taken by the assigned class teacher. You still have
          subject-teacher assignments, but they don&apos;t allow marking attendance. Ask
          your school admin if this looks wrong.
        </StatusBanner>
      ) : null}

      <PageSection className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:max-w-2xl">
          <Select
            value={classId}
            onChange={(e) => setClassId(e.target.value)}
            label="Class"
            disabled={isLoadingAssignments || classesForTeacher.length === 0}
          >
            <option value="" disabled>
              {isLoadingAssignments ? "Loading classes..." : "Select a class"}
            </option>
            {classesForTeacher.map((c) => (
              <option key={c.classId} value={c.classId}>
                {c.className}{c.section ? ` ${c.section}` : ""}
              </option>
            ))}
          </Select>
          <Input
            type="date"
            label="Date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            disabled={!classId}
          />
        </div>

        {isLoadingContext ? (
          <div className="flex min-h-96 items-center justify-center">
            <Spinner size="lg" />
          </div>
        ) : contextError ? (
          <ErrorState title="Error" message={contextError} onRetry={fetchContext} />
        ) : !context || context.students.length === 0 ? (
          <EmptyState
            title="No students"
            description="No active students found for this class."
            icon={<CalendarDays className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
          />
        ) : (
          <div className="space-y-4">
            {context.pendingLeaves.length > 0 && (
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-lg">Leave requests (pending)</CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  {context.pendingLeaves.map((leave) => (
                    <div
                      key={leave.leaveId}
                      className="flex flex-col gap-2 rounded-lg border p-3 sm:flex-row sm:items-start sm:justify-between"
                    >
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-medium">{leave.studentName}</span>
                          <Badge variant="secondary">{leave.rollNumber}</Badge>
                          <Badge variant="outline">Pending</Badge>
                        </div>
                        <div className="mt-1 text-sm text-muted-foreground">
                          {leave.startDate} → {leave.endDate}
                        </div>
                        <div className="mt-1 text-sm text-muted-foreground">{leave.reason}</div>
                      </div>
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          onClick={() => approveLeave(leave.leaveId)}
                          disabled={leaveActionId === leave.leaveId}
                        >
                          {leaveActionId === leave.leaveId ? <Spinner size="sm" /> : (
                            <>
                              <CheckCircle2 className="h-4 w-4" />
                              Approve
                            </>
                          )}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => rejectLeave(leave.leaveId)}
                          disabled={leaveActionId === leave.leaveId}
                        >
                          {leaveActionId === leave.leaveId ? <Spinner size="sm" /> : (
                            <>
                              <XCircle className="h-4 w-4" />
                              Reject
                            </>
                          )}
                        </Button>
                      </div>
                    </div>
                  ))}
                </CardContent>
              </Card>
            )}

            <Card>
              <CardHeader className="pb-2">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle className="text-lg">Take attendance</CardTitle>
                    {hasSaved && editingStudentIds.size === 0 ? (
                      <Badge variant="default" aria-label="All attendance saved">
                        <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
                        Saved
                      </Badge>
                    ) : null}
                  </div>
                  <Button
                    size="sm"
                    onClick={saveAttendance}
                    disabled={isSaving || (hasSaved && editingStudentIds.size === 0)}
                    aria-live="polite"
                  >
                    {isSaving ? (
                      <Spinner size="sm" />
                    ) : hasSaved && editingStudentIds.size === 0 ? (
                      <>
                        <CheckCircle2 className="h-4 w-4" />
                        Saved
                      </>
                    ) : (
                      "Save attendance"
                    )}
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                {context.students.map((s) => {
                  const onLeave = isStudentOnApprovedLeave(s.id);
                  const savedAndLocked = isRowLocked(s.id);
                  const disabled = onLeave || savedAndLocked;
                  const currentStatus = statusByStudentId[s.id] ?? "Present";

                  return (
                    <div
                      key={s.id}
                      className="flex flex-col gap-3 rounded-lg border p-3 sm:flex-row sm:items-center sm:justify-between"
                    >
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="truncate font-medium">{s.name}</span>
                          <Badge variant="secondary">{s.rollNumber}</Badge>
                          {onLeave ? <Badge variant="outline">On leave</Badge> : null}
                          {savedAndLocked && !onLeave ? (
                            <Badge variant="default" aria-label="Attendance saved">
                              Saved
                            </Badge>
                          ) : null}
                        </div>
                      </div>

                      <div className="flex flex-1 flex-col gap-2 sm:max-w-xl sm:flex-row sm:items-center sm:justify-end">
                        <div className="flex flex-wrap gap-2">
                          <Button
                            type="button"
                            size="sm"
                            variant={currentStatus === "Present" ? "default" : "outline"}
                            onClick={() => setStatusByStudentId((p) => ({ ...p, [s.id]: "Present" }))}
                            disabled={disabled}
                          >
                            <CheckCircle2 className="h-4 w-4" />
                            Present
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant={currentStatus === "Absent" ? "destructive" : "outline"}
                            onClick={() => setStatusByStudentId((p) => ({ ...p, [s.id]: "Absent" }))}
                            disabled={disabled}
                          >
                            <XCircle className="h-4 w-4" />
                            Absent
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant={currentStatus === "Late" ? "secondary" : "outline"}
                            onClick={() => setStatusByStudentId((p) => ({ ...p, [s.id]: "Late" }))}
                            disabled={disabled}
                          >
                            <Clock className="h-4 w-4" />
                            Late
                          </Button>
                          {savedAndLocked && !onLeave ? (
                            <Button
                              type="button"
                              size="sm"
                              variant="outline"
                              onClick={() => unlockStudentForEdit(s.id)}
                              aria-label={`Edit attendance for ${s.name}`}
                            >
                              <Pencil className="h-4 w-4" />
                              Edit
                            </Button>
                          ) : null}
                        </div>

                        {currentStatus !== "Present" ? (
                          <Input
                            label="Reason (optional)"
                            value={reasonByStudentId[s.id] ?? ""}
                            onChange={(e) =>
                              setReasonByStudentId((p) => ({ ...p, [s.id]: e.target.value }))
                            }
                            disabled={disabled}
                          />
                        ) : null}
                      </div>
                    </div>
                  );
                })}
              </CardContent>
            </Card>
          </div>
        )}
      </PageSection>
    </PageShell>
  );
}
