"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { GraduationCap, Plus, Trash2 } from "lucide-react";
import type {
  CreateExamRequest,
  CreateExamResponse,
  ExamSubjectInput,
} from "@/lib/types/exam";
import type { TeacherClassItem } from "@/lib/types/teacher";

interface SubjectRow extends ExamSubjectInput {
  _key: string;
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

// .NET's default System.Text.Json TimeOnly converter expects "HH:mm:ss".
// <input type="time"> emits "HH:mm", which makes the API body parse fail with
// "Failed to read parameter ... as JSON." Pad on submit to keep the form UX
// simple while staying compatible with the API contract.
function toApiTime(value: string): string {
  if (!value) return value;
  // Already "HH:mm:ss" or longer (some browsers include seconds)
  if (value.length >= 8) return value;
  return `${value}:00`;
}

function defaultAcademicYear(): string {
  // House convention is "2025-26". April onwards is a new academic year in
  // Indian schools, so roll over after March.
  const now = new Date();
  const year = now.getFullYear();
  const start = now.getMonth() >= 3 ? year : year - 1;
  const end = (start + 1) % 100;
  return `${start}-${end.toString().padStart(2, "0")}`;
}

export default function TeacherExamCreatePage(): React.ReactElement {
  const router = useRouter();

  const [assignments, setAssignments] = React.useState<TeacherClassItem[]>([]);
  const [isLoadingAssignments, setIsLoadingAssignments] = React.useState(true);
  const [assignmentError, setAssignmentError] = React.useState("");

  const [classId, setClassId] = React.useState("");
  const [name, setName] = React.useState("");
  const [academicYear, setAcademicYear] = React.useState(defaultAcademicYear());
  const [rows, setRows] = React.useState<SubjectRow[]>([newSubjectRow()]);

  const [submitError, setSubmitError] = React.useState("");
  const [isSubmitting, setIsSubmitting] = React.useState(false);

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

  React.useEffect(() => {
    void fetchAssignments();
  }, [fetchAssignments]);

  // Only class-teacher assignments can create exams — API will reject
  // anything else, so pre-filter here to keep the dropdown honest.
  const classTeacherClasses = React.useMemo(() => {
    const map = new Map<string, { classId: string; className: string; section: string }>();
    for (const a of assignments) {
      if (!a.isClassTeacher) continue;
      if (!map.has(a.classId)) {
        map.set(a.classId, {
          classId: a.classId,
          className: a.className,
          section: a.section,
        });
      }
    }
    return Array.from(map.values()).sort((a, b) => {
      const nameCompare = a.className.localeCompare(b.className);
      if (nameCompare !== 0) return nameCompare;
      return a.section.localeCompare(b.section);
    });
  }, [assignments]);

  React.useEffect(() => {
    if (classId) return;
    if (classTeacherClasses.length === 0) return;
    setClassId(classTeacherClasses[0]?.classId ?? "");
  }, [classTeacherClasses, classId]);

  const updateRow = (key: string, patch: Partial<SubjectRow>): void => {
    setRows((prev) =>
      prev.map((r) => (r._key === key ? { ...r, ...patch } : r))
    );
  };

  const addRow = (): void => setRows((prev) => [...prev, newSubjectRow()]);

  const removeRow = (key: string): void => {
    setRows((prev) => (prev.length <= 1 ? prev : prev.filter((r) => r._key !== key)));
  };

  const clientValidate = (): string => {
    if (!classId) return "Pick a class.";
    if (!name.trim()) return "Exam name is required.";
    if (!academicYear.trim()) return "Academic year is required.";
    if (rows.length === 0) return "At least one subject is required.";
    for (const r of rows) {
      if (!r.subject.trim()) return "Every subject needs a name.";
      if (!r.examDate) return `Pick a date for ${r.subject}.`;
      if (!r.startTime) return `Pick a start time for ${r.subject}.`;
      if (!r.endTime) return `Pick an end time for ${r.subject}.`;
      if (r.endTime <= r.startTime)
        return `${r.subject}: end time must be after start time.`;
      if (!Number.isFinite(r.maxMarks) || r.maxMarks <= 0)
        return `${r.subject}: max marks must be a positive number.`;
    }
    // Duplicate subject names are almost always a typo
    const seen = new Set<string>();
    for (const r of rows) {
      const key = r.subject.trim().toLowerCase();
      if (seen.has(key)) return `Duplicate subject: ${r.subject}.`;
      seen.add(key);
    }
    return "";
  };

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setSubmitError("");

    const msg = clientValidate();
    if (msg) {
      setSubmitError(msg);
      return;
    }

    const payload: CreateExamRequest = {
      classId,
      name: name.trim(),
      academicYear: academicYear.trim(),
      subjects: rows.map(({ _key, ...rest }) => ({
        ...rest,
        subject: rest.subject.trim(),
        startTime: toApiTime(rest.startTime),
        endTime: toApiTime(rest.endTime),
        room: rest.room?.trim() || null,
      })),
    };

    setIsSubmitting(true);
    try {
      const response = await apiPost<CreateExamResponse>(API_ENDPOINTS.exams, payload);
      router.push(`/teacher/exams/${response.examId}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : "Failed to create exam.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title="Create exam"
        description="Define the subjects, dates, and max marks for this exam. You'll review it before publishing to parents."
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        backAction={
          <Link href="/teacher/exams" prefetch={false}>
            <Button variant="ghost" size="sm">
              Back to exams
            </Button>
          </Link>
        }
      />

      {assignmentError ? (
        <StatusBanner variant="error">{assignmentError}</StatusBanner>
      ) : null}

      {isLoadingAssignments ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : classTeacherClasses.length === 0 ? (
        <PageSection>
          <p className="text-sm text-muted-foreground">
            Only the class teacher can create exams. You aren&apos;t marked as the class
            teacher for any class right now.
          </p>
        </PageSection>
      ) : (
        <PageSection>
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="grid gap-3 md:grid-cols-3">
              <Select
                label="Class"
                value={classId}
                onChange={(e) => setClassId(e.target.value)}
                disabled={isSubmitting}
              >
                <option value="" disabled>
                  Select a class
                </option>
                {classTeacherClasses.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    {c.className}
                    {c.section ? ` ${c.section}` : ""}
                  </option>
                ))}
              </Select>
              <Input
                id="exam-name"
                label="Exam name"
                placeholder="e.g. Term 1 Assessment"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={isSubmitting}
              />
              <Input
                id="exam-year"
                label="Academic year"
                placeholder="2025-26"
                value={academicYear}
                onChange={(e) => setAcademicYear(e.target.value)}
                disabled={isSubmitting}
              />
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-semibold">Subjects &amp; schedule</h3>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={addRow}
                  disabled={isSubmitting}
                >
                  <Plus className="h-4 w-4" />
                  Add subject
                </Button>
              </div>

              <div className="space-y-3">
                {rows.map((row, idx) => (
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
                        disabled={isSubmitting || rows.length <= 1}
                        aria-label="Remove subject"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                    <div className="grid gap-3 md:grid-cols-6">
                      <Input
                        id={`subject-${row._key}`}
                        label="Subject"
                        placeholder="e.g. Mathematics"
                        value={row.subject}
                        onChange={(e) =>
                          updateRow(row._key, { subject: e.target.value })
                        }
                        disabled={isSubmitting}
                        className="md:col-span-2"
                      />
                      <Input
                        id={`date-${row._key}`}
                        label="Exam date"
                        type="date"
                        value={row.examDate}
                        onChange={(e) =>
                          updateRow(row._key, { examDate: e.target.value })
                        }
                        disabled={isSubmitting}
                      />
                      <Input
                        id={`start-${row._key}`}
                        label="Start time"
                        type="time"
                        value={row.startTime}
                        onChange={(e) =>
                          updateRow(row._key, { startTime: e.target.value })
                        }
                        disabled={isSubmitting}
                      />
                      <Input
                        id={`end-${row._key}`}
                        label="End time"
                        type="time"
                        value={row.endTime}
                        onChange={(e) =>
                          updateRow(row._key, { endTime: e.target.value })
                        }
                        disabled={isSubmitting}
                      />
                      <Input
                        id={`marks-${row._key}`}
                        label="Max marks"
                        type="number"
                        min={1}
                        value={row.maxMarks}
                        onChange={(e) =>
                          updateRow(row._key, {
                            maxMarks: Number(e.target.value) || 0,
                          })
                        }
                        disabled={isSubmitting}
                      />
                      <Input
                        id={`room-${row._key}`}
                        label="Room (optional)"
                        placeholder="e.g. A-201"
                        value={row.room ?? ""}
                        onChange={(e) => updateRow(row._key, { room: e.target.value })}
                        disabled={isSubmitting}
                        className="md:col-span-6"
                      />
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {submitError ? (
              <StatusBanner variant="error">{submitError}</StatusBanner>
            ) : null}

            <div className="flex gap-2">
              <Button type="submit" size="sm" disabled={isSubmitting}>
                {isSubmitting ? <Spinner size="sm" /> : "Create exam"}
              </Button>
              <Link href="/teacher/exams" prefetch={false}>
                <Button type="button" size="sm" variant="outline" disabled={isSubmitting}>
                  Cancel
                </Button>
              </Link>
            </div>
          </form>
        </PageSection>
      )}
    </PageShell>
  );
}
