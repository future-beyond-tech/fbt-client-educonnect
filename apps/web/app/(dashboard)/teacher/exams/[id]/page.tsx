"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  ApiError,
  apiDelete,
  apiGet,
  apiPut,
} from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { GraduationCap, Pencil, Plus, Trash2 } from "lucide-react";
import type {
  ExamDetail,
  ExamSubjectInput,
  PublishExamScheduleResponse,
  UpdateExamRequest,
  UpdateExamResponse,
} from "@/lib/types/exam";

interface SubjectRow extends ExamSubjectInput {
  _key: string;
}

function toRows(exam: ExamDetail): SubjectRow[] {
  return exam.subjects.map((s) => ({
    _key: s.id,
    subject: s.subject,
    examDate: s.examDate,
    startTime: s.startTime,
    endTime: s.endTime,
    maxMarks: s.maxMarks,
    room: s.room ?? "",
  }));
}

function newSubjectRow(): SubjectRow {
  return {
    _key: crypto.randomUUID(),
    subject: "",
    examDate: "",
    startTime: "",
    endTime: "",
    maxMarks: 100,
    room: "",
  };
}

export default function TeacherExamDetailPage(): React.ReactElement {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const examId = params?.id ?? "";

  const [exam, setExam] = React.useState<ExamDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const [successMessage, setSuccessMessage] = React.useState("");
  const [actionError, setActionError] = React.useState("");

  const [isEditing, setIsEditing] = React.useState(false);
  const [editName, setEditName] = React.useState("");
  const [editAcademicYear, setEditAcademicYear] = React.useState("");
  const [editRows, setEditRows] = React.useState<SubjectRow[]>([]);
  const [editError, setEditError] = React.useState("");
  const [isSaving, setIsSaving] = React.useState(false);

  const [isPublishing, setIsPublishing] = React.useState(false);
  const [isDeleting, setIsDeleting] = React.useState(false);

  const fetchExam = React.useCallback(async () => {
    if (!examId) return;
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<ExamDetail>(`${API_ENDPOINTS.exams}/${examId}`);
      setExam(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load exam.");
    } finally {
      setIsLoading(false);
    }
  }, [examId]);

  React.useEffect(() => {
    void fetchExam();
  }, [fetchExam]);

  const startEdit = (): void => {
    if (!exam) return;
    setEditName(exam.name);
    setEditAcademicYear(exam.academicYear);
    setEditRows(toRows(exam));
    setEditError("");
    setIsEditing(true);
  };

  const cancelEdit = (): void => {
    setIsEditing(false);
    setEditError("");
  };

  const updateRow = (key: string, patch: Partial<SubjectRow>): void => {
    setEditRows((prev) => prev.map((r) => (r._key === key ? { ...r, ...patch } : r)));
  };

  const addRow = (): void => setEditRows((prev) => [...prev, newSubjectRow()]);

  const removeRow = (key: string): void => {
    setEditRows((prev) => (prev.length <= 1 ? prev : prev.filter((r) => r._key !== key)));
  };

  const validateEdit = (): string => {
    if (!editName.trim()) return "Exam name is required.";
    if (!editAcademicYear.trim()) return "Academic year is required.";
    if (editRows.length === 0) return "At least one subject is required.";
    for (const r of editRows) {
      if (!r.subject.trim()) return "Every subject needs a name.";
      if (!r.examDate) return `Pick a date for ${r.subject}.`;
      if (!r.startTime || !r.endTime) return `Pick times for ${r.subject}.`;
      if (r.endTime <= r.startTime)
        return `${r.subject}: end time must be after start time.`;
      if (!Number.isFinite(r.maxMarks) || r.maxMarks <= 0)
        return `${r.subject}: max marks must be a positive number.`;
    }
    const seen = new Set<string>();
    for (const r of editRows) {
      const key = r.subject.trim().toLowerCase();
      if (seen.has(key)) return `Duplicate subject: ${r.subject}.`;
      seen.add(key);
    }
    return "";
  };

  const handleSave = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setEditError("");
    const msg = validateEdit();
    if (msg) {
      setEditError(msg);
      return;
    }
    const payload: UpdateExamRequest = {
      name: editName.trim(),
      academicYear: editAcademicYear.trim(),
      subjects: editRows.map(({ _key, ...rest }) => ({
        ...rest,
        subject: rest.subject.trim(),
        room: rest.room?.trim() || null,
      })),
    };
    setIsSaving(true);
    try {
      const response = await apiPut<UpdateExamResponse>(
        `${API_ENDPOINTS.exams}/${examId}`,
        payload
      );
      setSuccessMessage(response.message);
      setIsEditing(false);
      await fetchExam();
    } catch (err) {
      setEditError(err instanceof ApiError ? err.message : "Failed to save exam.");
    } finally {
      setIsSaving(false);
    }
  };

  const handlePublish = async (): Promise<void> => {
    if (!exam) return;
    const confirmed = window.confirm(
      `Publish this schedule? Parents of students in ${formatClassLabel(exam)} will be notified immediately by in-app alert and email.`
    );
    if (!confirmed) return;
    setActionError("");
    setSuccessMessage("");
    setIsPublishing(true);
    try {
      const response = await apiPut<PublishExamScheduleResponse>(
        `${API_ENDPOINTS.exams}/${examId}/publish-schedule`,
        {}
      );
      setSuccessMessage(
        `${response.message} Notified ${response.notifiedParentCount} parent${
          response.notifiedParentCount === 1 ? "" : "s"
        }.`
      );
      await fetchExam();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to publish schedule.");
    } finally {
      setIsPublishing(false);
    }
  };

  const handleDelete = async (): Promise<void> => {
    const confirmed = window.confirm(
      "Delete this exam? This cannot be undone. Results can't be deleted once finalized."
    );
    if (!confirmed) return;
    setActionError("");
    setIsDeleting(true);
    try {
      await apiDelete<{ message: string }>(`${API_ENDPOINTS.exams}/${examId}`);
      router.push("/teacher/exams");
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to delete exam.");
      setIsDeleting(false);
    }
  };

  const formatClassLabel = (e: { className: string; section: string }): string => {
    const name = (e.className ?? "").trim();
    const section = (e.section ?? "").trim();
    if (!name && !section) return "";
    return section ? `${name} ${section}` : name;
  };

  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  const formatTime = (t: string): string => {
    const [h, m] = t.split(":").map(Number);
    if (!Number.isFinite(h) || !Number.isFinite(m)) return t;
    const d = new Date();
    d.setHours(h ?? 0, m ?? 0, 0, 0);
    return d.toLocaleTimeString("en-IN", {
      hour: "2-digit",
      minute: "2-digit",
      hour12: true,
    });
  };

  const getStatusBadge = (): React.ReactElement => {
    if (!exam) return <Badge variant="secondary">—</Badge>;
    if (exam.isResultsFinalized) return <Badge variant="default">Results finalized</Badge>;
    if (exam.isSchedulePublished) return <Badge variant="outline">Schedule published</Badge>;
    return <Badge variant="secondary">Draft</Badge>;
  };

  if (isLoading) {
    return (
      <PageShell>
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      </PageShell>
    );
  }

  if (error || !exam) {
    return (
      <PageShell>
        <ErrorState
          title="Could not load exam"
          message={error || "Exam not found."}
          onRetry={fetchExam}
        />
      </PageShell>
    );
  }

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title={exam.name}
        description={`${formatClassLabel(exam)} · ${exam.academicYear}`}
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        backAction={
          <Link href="/teacher/exams" prefetch={false}>
            <Button variant="ghost" size="sm">
              Back to exams
            </Button>
          </Link>
        }
        actions={
          <div className="flex flex-wrap items-center gap-2">
            {exam.canEditSchedule && !isEditing ? (
              <Button size="sm" variant="outline" onClick={startEdit}>
                <Pencil className="h-4 w-4" />
                Edit
              </Button>
            ) : null}
            {!exam.isSchedulePublished && exam.canEditSchedule ? (
              <Button size="sm" onClick={handlePublish} disabled={isPublishing}>
                {isPublishing ? <Spinner size="sm" /> : "Publish schedule"}
              </Button>
            ) : null}
            {exam.isSchedulePublished ? (
              <Link href={`/teacher/exams/${exam.examId}/results`} prefetch={false}>
                <Button size="sm">
                  {exam.isResultsFinalized ? "View results" : "Enter results"}
                </Button>
              </Link>
            ) : null}
            {!exam.isResultsFinalized ? (
              <Button
                size="sm"
                variant="outline"
                onClick={handleDelete}
                disabled={isDeleting}
              >
                <Trash2 className="h-4 w-4" />
                Delete
              </Button>
            ) : null}
          </div>
        }
        stats={[
          { label: "Status", value: exam.isResultsFinalized ? "Finalized" : exam.isSchedulePublished ? "Published" : "Draft" },
          { label: "Subjects", value: exam.subjects.length.toString() },
        ]}
      />

      {successMessage ? (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      ) : null}
      {actionError ? (
        <StatusBanner variant="error">{actionError}</StatusBanner>
      ) : null}

      <PageSection className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          {getStatusBadge()}
          {exam.schedulePublishedAt ? (
            <span className="text-sm text-muted-foreground">
              Schedule published{" "}
              {new Date(exam.schedulePublishedAt).toLocaleString("en-IN", {
                day: "numeric",
                month: "short",
                hour: "2-digit",
                minute: "2-digit",
              })}
            </span>
          ) : null}
          {exam.resultsFinalizedAt ? (
            <span className="text-sm text-muted-foreground">
              Results finalized{" "}
              {new Date(exam.resultsFinalizedAt).toLocaleString("en-IN", {
                day: "numeric",
                month: "short",
                hour: "2-digit",
                minute: "2-digit",
              })}
            </span>
          ) : null}
        </div>
        {exam.isSchedulePublished && !exam.isResultsFinalized ? (
          <p className="text-sm text-muted-foreground">
            Schedule is locked once published. If you need to amend it, delete and
            recreate, or post a notice explaining the change.
          </p>
        ) : null}
      </PageSection>

      {isEditing ? (
        <PageSection>
          <form onSubmit={handleSave} className="space-y-5">
            <h3 className="text-lg font-semibold">Edit exam</h3>
            <div className="grid gap-3 md:grid-cols-2">
              <Input
                id="edit-name"
                label="Exam name"
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                disabled={isSaving}
              />
              <Input
                id="edit-year"
                label="Academic year"
                value={editAcademicYear}
                onChange={(e) => setEditAcademicYear(e.target.value)}
                disabled={isSaving}
              />
            </div>
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h4 className="font-medium">Subjects</h4>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={addRow}
                  disabled={isSaving}
                >
                  <Plus className="h-4 w-4" />
                  Add subject
                </Button>
              </div>
              <div className="space-y-3">
                {editRows.map((row, idx) => (
                  <div
                    key={row._key}
                    className="rounded-2xl border border-border/70 bg-muted/30 p-4"
                  >
                    <div className="mb-3 flex items-center justify-between">
                      <span className="text-sm font-medium text-muted-foreground">
                        Subject {idx + 1}
                      </span>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        onClick={() => removeRow(row._key)}
                        disabled={isSaving || editRows.length <= 1}
                        aria-label="Remove subject"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                    <div className="grid gap-3 md:grid-cols-6">
                      <Input
                        id={`e-subject-${row._key}`}
                        label="Subject"
                        value={row.subject}
                        onChange={(e) => updateRow(row._key, { subject: e.target.value })}
                        disabled={isSaving}
                        className="md:col-span-2"
                      />
                      <Input
                        id={`e-date-${row._key}`}
                        label="Date"
                        type="date"
                        value={row.examDate}
                        onChange={(e) => updateRow(row._key, { examDate: e.target.value })}
                        disabled={isSaving}
                      />
                      <Input
                        id={`e-start-${row._key}`}
                        label="Start"
                        type="time"
                        value={row.startTime}
                        onChange={(e) => updateRow(row._key, { startTime: e.target.value })}
                        disabled={isSaving}
                      />
                      <Input
                        id={`e-end-${row._key}`}
                        label="End"
                        type="time"
                        value={row.endTime}
                        onChange={(e) => updateRow(row._key, { endTime: e.target.value })}
                        disabled={isSaving}
                      />
                      <Input
                        id={`e-marks-${row._key}`}
                        label="Max marks"
                        type="number"
                        min={1}
                        value={row.maxMarks}
                        onChange={(e) =>
                          updateRow(row._key, {
                            maxMarks: Number(e.target.value) || 0,
                          })
                        }
                        disabled={isSaving}
                      />
                      <Input
                        id={`e-room-${row._key}`}
                        label="Room (optional)"
                        value={row.room ?? ""}
                        onChange={(e) => updateRow(row._key, { room: e.target.value })}
                        disabled={isSaving}
                        className="md:col-span-6"
                      />
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {editError ? <StatusBanner variant="error">{editError}</StatusBanner> : null}

            <div className="flex gap-2">
              <Button type="submit" size="sm" disabled={isSaving}>
                {isSaving ? <Spinner size="sm" /> : "Save changes"}
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={cancelEdit}
                disabled={isSaving}
              >
                Cancel
              </Button>
            </div>
          </form>
        </PageSection>
      ) : (
        <PageSection className="space-y-3">
          <h3 className="text-lg font-semibold">Schedule</h3>
          {exam.subjects
            .slice()
            .sort((a, b) => {
              if (a.examDate !== b.examDate) return a.examDate.localeCompare(b.examDate);
              return a.startTime.localeCompare(b.startTime);
            })
            .map((s) => (
              <Card key={s.id}>
                <CardHeader className="pb-2">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <CardTitle className="text-base">{s.subject}</CardTitle>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {formatDate(s.examDate)} · {formatTime(s.startTime)}–
                        {formatTime(s.endTime)}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="secondary">Max {s.maxMarks}</Badge>
                      {s.room ? <Badge variant="outline">Room {s.room}</Badge> : null}
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pt-0" />
              </Card>
            ))}
        </PageSection>
      )}
    </PageShell>
  );
}
