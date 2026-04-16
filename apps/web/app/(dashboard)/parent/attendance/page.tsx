"use client";

import * as React from "react";
import { useAuth } from "@/hooks/use-auth";
import { apiDelete, apiGet, apiPost, apiPut, ApiError } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import type {
  AttendanceRecord,
  LeaveApplication,
  ApplyLeaveRequest,
  UpdateLeaveRequest,
  GetLeaveApplicationsResponse,
} from "@/lib/types/attendance";
import type { ParentChildItem } from "@/lib/types/student";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { CalendarDays, PlusCircle, X } from "lucide-react";

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

type TabId = "absences" | "leaves";

// ─── Leave Application Form (slide-over) ───────────────────────────────────

interface LeaveFormProps {
  onClose: () => void;
  onSuccess: () => void;
  initialLeave?: LeaveApplication | null;
}

function LeaveApplicationForm({
  onClose,
  onSuccess,
  initialLeave,
}: LeaveFormProps): React.ReactElement {
  const today = new Date().toISOString().slice(0, 10);
  const titleId = React.useId();
  const descriptionId = React.useId();
  const [studentId, setStudentId] = React.useState(initialLeave?.studentId ?? "");
  const [startDate, setStartDate] = React.useState(initialLeave?.startDate ?? today);
  const [endDate, setEndDate] = React.useState(initialLeave?.endDate ?? today);
  const [reason, setReason] = React.useState(initialLeave?.reason ?? "");
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");

  // Fetch linked students so the parent can pick which child
  const [students, setStudents] = React.useState<ParentChildItem[]>([]);
  const [loadingStudents, setLoadingStudents] = React.useState(true);

  React.useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    const previousPaddingRight = document.body.style.paddingRight;
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;

    document.body.style.overflow = "hidden";
    if (scrollbarWidth > 0) {
      document.body.style.paddingRight = `${scrollbarWidth}px`;
    }

    const handleKeyDown = (event: KeyboardEvent): void => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.body.style.paddingRight = previousPaddingRight;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [onClose]);

  React.useEffect(() => {
    apiGet<ParentChildItem[]>(API_ENDPOINTS.studentsMyChildren)
      .then((data) => {
        setStudents(data);
        if (initialLeave?.studentId) {
          // Keep the student locked to the original leave request when editing.
          setStudentId(initialLeave.studentId);
          return;
        }

        const first = data[0];
        if (first) {
          setStudentId(first.id);
          return;
        }

        setError("No linked students were found for this parent account.");
      })
      .catch((err) =>
        setError(
          err instanceof ApiError ? err.message : "Failed to load student details."
        )
      )
      .finally(() => setLoadingStudents(false));
  }, [initialLeave?.studentId]);

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError("");

    if (endDate < startDate) {
      setError("End date cannot be before start date.");
      return;
    }

    if (!studentId) {
      setError("Please select a child before submitting the leave request.");
      return;
    }

    setIsSubmitting(true);
    try {
      if (initialLeave?.id) {
        const payload: UpdateLeaveRequest = { startDate, endDate, reason };
        await apiPut(`${API_ENDPOINTS.leaveApplications}/${initialLeave.id}`, payload);
      } else {
        const payload: ApplyLeaveRequest = { studentId, startDate, endDate, reason };
        await apiPost(API_ENDPOINTS.leaveApplications, payload);
      }
      onSuccess();
      onClose();
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to submit leave application.";
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 bg-slate-950/58 backdrop-blur-[3px]"
      onClick={onClose}
    >
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top,rgb(var(--glow-1)/0.14),transparent_34rem)]" />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        className="absolute inset-x-0 bottom-0 flex h-[min(46rem,calc(100dvh-0.75rem))] w-full flex-col overflow-hidden rounded-t-[32px] border border-border/75 bg-[linear-gradient(180deg,rgba(255,255,255,0.995),rgba(244,248,251,0.995))] shadow-[0_-18px_80px_-30px_rgba(10,14,24,0.84)] dark:bg-[linear-gradient(180deg,rgba(12,30,48,0.995),rgba(8,18,31,0.995))] sm:inset-y-4 sm:bottom-auto sm:left-auto sm:right-4 sm:h-auto sm:max-h-[calc(100dvh-2rem)] sm:max-w-[32rem] sm:rounded-[32px] sm:shadow-[0_34px_90px_-40px_rgba(10,14,24,0.76)]"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mx-auto mt-3 h-1.5 w-14 rounded-full bg-slate-300/85 sm:hidden" />

        <div className="relative border-b border-border/70 bg-card/88 px-5 pb-5 pt-4 backdrop-blur-sm dark:bg-card/96 sm:px-6 sm:pt-5">
          <div className="pointer-events-none absolute inset-x-0 top-0 h-24 bg-[radial-gradient(circle_at_top,rgb(var(--glow-1)/0.14),transparent_72%)]" />
          <div className="relative flex items-start justify-between gap-4">
            <div className="space-y-3">
              <span className="inline-flex rounded-full border border-primary/10 bg-primary/8 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.24em] text-primary/80">
                Family updates
              </span>
              <div className="space-y-1">
                <h2 id={titleId} className="text-xl font-semibold text-foreground">
                  Apply for Leave
                </h2>
                <p
                  id={descriptionId}
                  className="max-w-md text-sm leading-6 text-muted-foreground"
                >
                  Select your child, choose the dates, and share the reason for
                  the leave request.
                </p>
              </div>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={onClose}
              aria-label="Close"
              className="shrink-0 rounded-full bg-card/78 text-foreground shadow-[0_14px_32px_-26px_rgba(15,40,69,0.5)] hover:bg-card"
            >
              <X className="h-5 w-5" />
            </Button>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
          <div className="flex-1 space-y-5 overflow-y-auto px-5 py-5 sm:px-6 sm:py-6">
            <div className="rounded-[26px] border border-border/75 bg-card/84 p-4 shadow-[0_22px_44px_-34px_rgba(15,40,69,0.42)] dark:bg-card/92">
              <p className="text-xs font-semibold uppercase tracking-[0.22em] text-primary/80">
                Request details
              </p>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                Leave requests are sent for review and will appear in your leave
                history after submission.
              </p>
            </div>

            {loadingStudents ? (
              <div className="flex min-h-20 items-center gap-3 rounded-[26px] border border-dashed border-border/80 bg-card/75 px-4 py-4 text-sm text-muted-foreground dark:bg-card/88">
                <Spinner size="sm" />
                Loading children...
              </div>
            ) : students.length > 0 ? (
              <Select
                label="Child"
                value={studentId}
                onChange={(e) => setStudentId(e.target.value)}
                className="bg-card/96 backdrop-blur-none"
                disabled={!!initialLeave?.id}
                required
              >
                {students.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name} ({s.rollNumber})
                  </option>
                ))}
              </Select>
            ) : (
              <div className="rounded-[26px] border border-dashed border-border/80 bg-card/75 px-4 py-4 dark:bg-card/88">
                <p className="text-sm font-medium text-foreground">
                  No linked students available
                </p>
                <p className="mt-1 text-sm leading-6 text-muted-foreground">
                  This account does not have any linked children yet, so a leave
                  request cannot be submitted.
                </p>
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                type="date"
                label="Start Date"
                value={startDate}
                min={today}
                className="bg-card/96 backdrop-blur-none"
                onChange={(e) => {
                  setStartDate(e.target.value);
                  if (endDate < e.target.value) setEndDate(e.target.value);
                }}
                required
              />

              <Input
                type="date"
                label="End Date"
                value={endDate}
                min={startDate}
                className="bg-card/96 backdrop-blur-none"
                onChange={(e) => setEndDate(e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Textarea
                label="Reason"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Please describe the reason for leave..."
                rows={5}
                maxLength={1000}
                className="bg-card/96 backdrop-blur-none"
                required
              />
              <p className="text-right text-xs text-muted-foreground">
                {reason.length}/1000
              </p>
            </div>
          </div>

          <div className="mt-auto space-y-3 border-t border-border/70 bg-card/94 px-5 pb-[calc(1rem+env(safe-area-inset-bottom))] pt-4 backdrop-blur-md dark:bg-card/98 sm:px-6 sm:pb-6">
            {error ? (
              <StatusBanner variant="error" className="backdrop-blur-none">
                {error}
              </StatusBanner>
            ) : null}

            <div className="flex flex-col gap-3 sm:flex-row">
              <Button
                type="button"
                variant="outline"
                onClick={onClose}
                className="w-full sm:flex-1"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={isSubmitting || loadingStudents || students.length === 0}
                className="w-full sm:flex-1"
              >
                {isSubmitting ? (
                  <Spinner size="sm" />
                ) : initialLeave?.id ? (
                  "Update Request"
                ) : (
                  "Submit Application"
                )}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Status badge helpers ───────────────────────────────────────────────────

function leaveStatusVariant(
  status: string
): "default" | "secondary" | "destructive" | "outline" {
  switch (status) {
    case "Approved":  return "default";
    case "Rejected":  return "destructive";
    default:          return "secondary";   // Pending
  }
}

// ─── Main page ──────────────────────────────────────────────────────────────

export default function ParentAttendancePage(): React.ReactElement {
  const { token, isLoading: isAuthLoading } = useAuth();
  // ── Tabs
  const [activeTab, setActiveTab] = React.useState<TabId>("absences");

  // ── Absence records state
  const [records, setRecords] = React.useState<AttendanceRecord[]>([]);
  const [isLoadingRecords, setIsLoadingRecords] = React.useState(true);
  const [recordsError, setRecordsError] = React.useState("");
  const [selectedMonth, setSelectedMonth] = React.useState(new Date().getMonth() + 1);
  const [selectedYear, setSelectedYear] = React.useState(new Date().getFullYear());

  // ── Leave applications state
  const [leaves, setLeaves] = React.useState<LeaveApplication[]>([]);
  const [isLoadingLeaves, setIsLoadingLeaves] = React.useState(false);
  const [leavesError, setLeavesError] = React.useState("");
  const [showLeaveForm, setShowLeaveForm] = React.useState(false);
  const [editingLeave, setEditingLeave] = React.useState<LeaveApplication | null>(null);

  // ── Fetch absence records
  const fetchAttendance = React.useCallback(async () => {
    setIsLoadingRecords(true);
    setRecordsError("");
    try {
      const data = await apiGet<AttendanceRecord[]>(
        `${API_ENDPOINTS.attendance}?month=${selectedMonth}&year=${selectedYear}`
      );
      setRecords(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setRecordsError(err.message || "Failed to load attendance records.");
      } else {
        setRecordsError("Failed to load attendance records.");
      }
    } finally {
      setIsLoadingRecords(false);
    }
  }, [selectedMonth, selectedYear]);

  // ── Fetch leave applications
  const fetchLeaves = React.useCallback(async () => {
    setIsLoadingLeaves(true);
    setLeavesError("");
    try {
      const data = await apiGet<GetLeaveApplicationsResponse>(
        API_ENDPOINTS.leaveApplications
      );
      setLeaves(data.items);
    } catch {
      setLeavesError("Failed to load leave applications.");
    } finally {
      setIsLoadingLeaves(false);
    }
  }, []);

  const handleCancelLeave = async (leaveId: string): Promise<void> => {
    const ok = window.confirm("Cancel this leave request? This cannot be undone.");
    if (!ok) return;

    try {
      await apiDelete(`${API_ENDPOINTS.leaveApplications}/${leaveId}`);
      fetchLeaves();
    } catch (err) {
      setLeavesError(
        err instanceof ApiError ? err.message : "Failed to cancel leave application."
      );
    }
  };

  React.useEffect(() => {
    if (isAuthLoading) return;
    if (!token) return;
    fetchAttendance();
  }, [fetchAttendance, isAuthLoading, token]);

  React.useEffect(() => {
    if (activeTab === "leaves") fetchLeaves();
  }, [activeTab, fetchLeaves]);

  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
    });
  };

  const formatDateRange = (start: string, end: string): string => {
    if (start === end) return formatDate(start);
    return `${formatDate(start)} – ${formatDate(end)}`;
  };

  // ── Tab button component
  const TabButton = ({ id, label }: { id: TabId; label: string }): React.ReactElement => (
    <button
      onClick={() => setActiveTab(id)}
      className={[
        "rounded-md px-4 py-2 text-sm font-medium transition-colors",
        activeTab === id
          ? "bg-primary text-primary-foreground"
          : "text-muted-foreground hover:text-foreground hover:bg-muted",
      ].join(" ")}
    >
      {label}
    </button>
  );

  return (
    <PageShell>
      <PageHeader
        eyebrow="Family updates"
        title="Attendance"
        description="Review recorded absences and keep leave requests organized for your child."
        icon={<CalendarDays className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <Button
            onClick={() => {
              setEditingLeave(null);
              setShowLeaveForm(true);
            }}
            className="shrink-0"
          >
            <PlusCircle className="h-4 w-4" />
            Apply Leave
          </Button>
        )}
        stats={[
          { label: "Absences", value: records.length.toString() },
          { label: "Leave requests", value: leaves.length.toString() },
        ]}
      />

      <PageSection className="space-y-5">
        <div className="inline-flex flex-wrap gap-2 rounded-full border border-border/70 bg-card/72 p-2 shadow-[0_16px_36px_-30px_rgba(15,40,69,0.45)] backdrop-blur-sm dark:bg-card/88">
          <TabButton id="absences" label="Absence Records" />
          <TabButton id="leaves" label="Leave Applications" />
        </div>

        {activeTab === "absences" && (
          <>
            <div className="grid gap-3 sm:grid-cols-2 lg:max-w-xl">
              <Select
                value={selectedMonth.toString()}
                onChange={(e) => setSelectedMonth(Number(e.target.value))}
                label="Month"
              >
                {MONTHS.map((name, i) => (
                  <option key={name} value={i + 1}>
                    {name}
                  </option>
                ))}
              </Select>
              <Select
                value={selectedYear.toString()}
                onChange={(e) => setSelectedYear(Number(e.target.value))}
                label="Year"
              >
                {[selectedYear - 1, selectedYear, selectedYear + 1].map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </Select>
            </div>

            {isLoadingRecords ? (
              <div className="flex min-h-96 items-center justify-center">
                <Spinner size="lg" />
              </div>
            ) : recordsError ? (
              <ErrorState title="Error" message={recordsError} onRetry={fetchAttendance} />
            ) : records.length === 0 ? (
              <EmptyState
                title="No absences recorded"
                description={`No absence records for ${MONTHS[selectedMonth - 1]} ${selectedYear}.`}
                icon={<CalendarDays className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
              />
            ) : (
              <div className="space-y-3">
                {records.map((record) => (
                  <Card key={record.recordId}>
                    <CardContent className="flex items-center justify-between gap-4 p-4">
                      <div className="min-w-0 space-y-1">
                        <p className="font-medium">{formatDate(record.date)}</p>
                        {record.reason && (
                          <p className="text-sm text-muted-foreground">{record.reason}</p>
                        )}
                      </div>
                      <div className="flex flex-wrap items-center justify-end gap-2">
                        <Badge variant="destructive">{record.status}</Badge>
                        <span className="text-xs text-muted-foreground">
                          by {record.enteredByRole}
                        </span>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            )}
          </>
        )}

        {activeTab === "leaves" && (
          <>
            {isLoadingLeaves ? (
              <div className="flex min-h-96 items-center justify-center">
                <Spinner size="lg" />
              </div>
            ) : leavesError ? (
              <ErrorState title="Error" message={leavesError} onRetry={fetchLeaves} />
            ) : leaves.length === 0 ? (
              <EmptyState
                title="No leave applications"
                description="You haven't submitted any leave applications yet."
                icon={<CalendarDays className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
              />
            ) : (
              <div className="space-y-3">
                {leaves.map((leave) => (
                  <Card key={leave.id}>
                    <CardContent className="p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0 flex-1 space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-medium">
                              {formatDateRange(leave.startDate, leave.endDate)}
                            </p>
                            {leave.studentName && (
                              <span className="text-xs text-muted-foreground">
                                · {leave.studentName}
                              </span>
                            )}
                          </div>
                          <p className="line-clamp-2 text-sm text-muted-foreground">
                            {leave.reason}
                          </p>
                          {leave.reviewNote && (
                            <p className="mt-1 text-sm text-foreground/70">
                              <span className="font-medium">Review note:</span> {leave.reviewNote}
                            </p>
                          )}
                        </div>
                        <div className="flex flex-col items-end gap-2">
                          <Badge variant={leaveStatusVariant(leave.status)} className="shrink-0">
                            {leave.status}
                          </Badge>
                          {leave.status === "Pending" ? (
                            <div className="flex flex-wrap gap-2">
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                  setEditingLeave(leave);
                                  setShowLeaveForm(true);
                                }}
                              >
                                Edit
                              </Button>
                              <Button
                                type="button"
                                variant="destructive"
                                size="sm"
                                onClick={() => handleCancelLeave(leave.id)}
                              >
                                Cancel
                              </Button>
                            </div>
                          ) : null}
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            )}
          </>
        )}
      </PageSection>

      {showLeaveForm && (
        <LeaveApplicationForm
          onClose={() => {
            setShowLeaveForm(false);
            setEditingLeave(null);
          }}
          onSuccess={fetchLeaves}
          initialLeave={editingLeave}
        />
      )}
    </PageShell>
  );
}
