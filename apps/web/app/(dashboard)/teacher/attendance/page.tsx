"use client";

import * as React from "react";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import {
  approveLeaveAction,
  rejectLeaveAction,
  submitAttendanceTakeAction,
} from "@/lib/actions/attendance-actions";
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
  // Server-side authority: true when the caller is the class teacher, false
  // when they're a subject teacher viewing attendance read-only.
  canEdit: boolean;
  students: TakeStudent[];
  exceptions: TakeException[];
  approvedLeaves: TakeLeave[];
  pendingLeaves: TakeLeave[];
}

// How many days back a teacher is allowed to mark attendance for. Kept in
// sync with SubmitAttendanceTakeCommandValidator.MaxBackdateDays on the
// backend — if you change one, change both. Future dates are never allowed.
const MAX_BACKDATE_DAYS = 7;

export default function TeacherAttendancePage(): React.ReactElement {
  const { token, isLoading: isAuthLoading } = useAuth();
  const today = React.useMemo(() => new Date().toISOString().slice(0, 10), []);
  const minDate = React.useMemo(() => {
    const d = new Date();
    d.setDate(d.getDate() - MAX_BACKDATE_DAYS);
    return d.toISOString().slice(0, 10);
  }, []);

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

  // Show every class the teacher is assigned to. Class-teacher classes are
  // editable; subject-teacher classes render the same screen read-only.
  // The server decides editability via response.canEdit — this flag is just
  // for ordering/labeling the dropdown.
  const classesForTeacher = React.useMemo(() => {
    const map = new Map<string, { classId: string; className: string; section: string; isClassTeacher: boolean }>();
    for (const a of assignments) {
      const existing = map.get(a.classId);
      if (existing) {
        existing.isClassTeacher = existing.isClassTeacher || a.isClassTeacher;
      } else {
        map.set(a.classId, {
          classId: a.classId,
          className: a.className,
          section: a.section,
          isClassTeacher: a.isClassTeacher,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => {
      // Surface class-teacher classes first so the default auto-select lands
      // on an editable class whenever one exists.
      if (a.isClassTeacher !== b.isClassTeacher) return a.isClassTeacher ? -1 : 1;
      return (a.className + a.section).localeCompare(b.className + b.section);
    });
  }, [assignments]);

  const canEdit = context?.canEdit ?? false;

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

  const hydrateFormFromContext = React.useCallback(
    (ctx: TakeContextResponse, options?: { preserveSaved?: boolean }) => {
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

      if (options?.preserveSaved) {
        // Called right after a successful save — the UI has ALREADY been
        // locked by saveAttendance(). Never undo that here: if the refetch
        // returns canEdit=false (e.g. the teacher's class assignment changed
        // between the POST and the GET), we'd otherwise unlock the rows and
        // silently tell the teacher their save didn't stick. Keep hasSaved
        // whatever it already is, just reset the per-row editing set.
        setEditingStudentIds(new Set());
      } else {
        // Initial load or class/date change. Derive the lock state purely
        // from what the server reports: exception records mean attendance has
        // been taken for this date, so we lock and offer Edit affordances.
        // Read-only (subject-teacher) contexts can never be locked because
        // they can never enter the save path.
        const hasServerExceptions = ctx.exceptions.length > 0;
        setHasSaved(ctx.canEdit && hasServerExceptions);
        setEditingStudentIds(new Set());
      }
    },
    []
  );

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
    if (!context.canEdit) {
      // Defensive guard — the UI should hide the Save button when !canEdit,
      // but never send a POST the backend will 403. Keep the check local so
      // the user gets an immediate, readable message if something slips.
      setActionError("You don't have permission to edit attendance for this class.");
      return;
    }
    setIsSaving(true);
    setActionError("");
    setActionSuccess("");

    try {
      const items = context.students.map((s) => ({
        studentId: s.id,
        status: statusByStudentId[s.id] ?? "Present",
        reason: (reasonByStudentId[s.id] ?? "").trim() || null,
      }));

      const actionResult = await submitAttendanceTakeAction({
        classId: context.classId,
        date: context.date,
        items,
      });

      if (!actionResult.ok) {
        setActionError(
          actionResult.formError ??
            Object.values(actionResult.fieldErrors ?? {})[0] ??
            "Failed to save attendance.",
        );
        return;
      }

      const res = actionResult.data;

      // Lock the UI IMMEDIATELY on a successful POST. Previously we waited
      // for the follow-up refetch to succeed before calling setHasSaved(true)
      // — if that refetch was slow, failed silently, or returned canEdit=false
      // the teacher would click Save and see no visual change, then assume
      // nothing happened. Locking here guarantees feedback as soon as the
      // server confirms the save, regardless of what the refetch does.
      setHasSaved(true);
      setEditingStudentIds(new Set());

      // Show the success banner right away for the same reason — don't make
      // it depend on the follow-up refetch.
      const fallbackMessage = "Attendance saved successfully.";
      setActionSuccess(res?.message?.trim() ? res.message : fallbackMessage);

      // Refetch the authoritative server state as a second-pass reconciliation.
      // If the server has adjusted any values (e.g. an approved leave was
      // processed concurrently), this keeps the UI in sync. The UI is already
      // locked by this point, so a failure here is non-fatal.
      try {
        const refreshed = await apiGet<TakeContextResponse>(
          `${API_ENDPOINTS.attendance}/take?classId=${context.classId}&date=${context.date}`
        );
        setContext(refreshed);
        hydrateFormFromContext(refreshed, { preserveSaved: true });
      } catch {
        // Refetch is best-effort — the POST already succeeded and the UI is
        // locked. Swallow the error silently so we don't confuse the teacher
        // with a misleading "save failed" message.
      }

      // Scroll the success banner into view — on mobile the teacher is often
      // scrolled down to the last student when they hit Save, and the banner
      // is rendered near the top of the page.
      if (typeof window !== "undefined") {
        window.scrollTo({ top: 0, behavior: "smooth" });
      }
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
      const result = await approveLeaveAction(leaveId);
      if (!result.ok) {
        setActionError(result.formError ?? "Failed to approve leave.");
        return;
      }
      setActionSuccess(result.data.message);
      await fetchContext();
    } catch {
      setActionError("Failed to approve leave.");
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
      const result = await rejectLeaveAction({ leaveApplicationId: leaveId, reviewNote: note });
      if (!result.ok) {
        setActionError(
          result.formError ??
            Object.values(result.fieldErrors ?? {})[0] ??
            "Failed to reject leave.",
        );
        return;
      }
      setActionSuccess(result.data.message);
      await fetchContext();
    } catch {
      setActionError("Failed to reject leave.");
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
      {context && !canEdit ? (
        <StatusBanner variant="info" title="Read-only view">
          You&apos;re viewing attendance for this class as a subject teacher. Only the
          assigned class teacher can mark, edit, or approve leave requests.
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
            // Attendance is only editable for the last N days, never the
            // future. The native date picker enforces this visually; the
            // onChange clamp below enforces it when a user types a date
            // manually (which bypasses min/max in most browsers).
            min={minDate}
            max={today}
            onChange={(e) => {
              const next = e.target.value;
              if (!next) return;
              if (next > today || next < minDate) {
                setActionError(
                  `Attendance can only be marked for dates between ${minDate} and ${today}.`
                );
                return;
              }
              setActionError("");
              setDate(next);
            }}
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
                      {canEdit ? (
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
                      ) : null}
                    </div>
                  ))}
                </CardContent>
              </Card>
            )}

            <Card>
              <CardHeader className="pb-2">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle className="text-lg">
                      {canEdit ? "Take attendance" : "Attendance"}
                    </CardTitle>
                    {!canEdit ? (
                      <Badge variant="outline" aria-label="Read-only">
                        Read-only
                      </Badge>
                    ) : null}
                    {canEdit && hasSaved && editingStudentIds.size === 0 ? (
                      <Badge variant="default" aria-label="All attendance saved">
                        <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
                        Saved
                      </Badge>
                    ) : null}
                  </div>
                  {canEdit ? (
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
                  ) : null}
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                {context.students.map((s) => {
                  const onLeave = isStudentOnApprovedLeave(s.id);
                  const savedAndLocked = isRowLocked(s.id);
                  // Read-only viewers (non-class-teachers) never get edit
                  // affordances regardless of save state.
                  const disabled = !canEdit || onLeave || savedAndLocked;
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
                          {canEdit && savedAndLocked && !onLeave ? (
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
                          {canEdit && savedAndLocked && !onLeave ? (
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
