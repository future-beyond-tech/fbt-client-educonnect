"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  ApiError,
  apiGet,
  apiPostMultipart,
  apiPut,
} from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { FileUp, GraduationCap } from "lucide-react";
import type {
  ExamResultRowInput,
  ExamResultsGrid,
  FinalizeExamResultsResponse,
  UploadExamResultsCsvResponse,
  UpsertExamResultsRequest,
  UpsertExamResultsResponse,
} from "@/lib/types/exam";

interface CellValue {
  marksObtained: string; // kept as string for the input; "" means null
  grade: string;
  remarks: string;
  isAbsent: boolean;
}

type CellKey = `${string}:${string}`; // `${studentId}:${examSubjectId}`
type CellMap = Record<CellKey, CellValue>;

function toCellMap(grid: ExamResultsGrid): CellMap {
  const next: CellMap = {};
  for (const student of grid.students) {
    for (const col of grid.subjects) {
      const existing = student.cells.find((c) => c.examSubjectId === col.examSubjectId);
      next[`${student.studentId}:${col.examSubjectId}`] = {
        marksObtained:
          existing && existing.marksObtained !== null
            ? String(existing.marksObtained)
            : "",
        grade: existing?.grade ?? "",
        remarks: existing?.remarks ?? "",
        isAbsent: existing?.isAbsent ?? false,
      };
    }
  }
  return next;
}

export default function TeacherExamResultsPage(): React.ReactElement {
  const params = useParams<{ id: string }>();
  const examId = params?.id ?? "";

  const [grid, setGrid] = React.useState<ExamResultsGrid | null>(null);
  const [cells, setCells] = React.useState<CellMap>({});
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const [isSaving, setIsSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState("");
  const [saveSuccess, setSaveSuccess] = React.useState("");
  const [saveWarnings, setSaveWarnings] = React.useState<string[]>([]);

  const [isUploading, setIsUploading] = React.useState(false);
  const [uploadError, setUploadError] = React.useState("");
  const [uploadSuccess, setUploadSuccess] = React.useState("");
  const [uploadWarnings, setUploadWarnings] = React.useState<string[]>([]);
  const fileInputRef = React.useRef<HTMLInputElement | null>(null);

  const [isFinalizing, setIsFinalizing] = React.useState(false);
  const [finalizeError, setFinalizeError] = React.useState("");
  const [finalizeSuccess, setFinalizeSuccess] = React.useState("");

  const fetchGrid = React.useCallback(async () => {
    if (!examId) return;
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<ExamResultsGrid>(
        `${API_ENDPOINTS.exams}/${examId}/results`
      );
      setGrid(data);
      setCells(toCellMap(data));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load results.");
    } finally {
      setIsLoading(false);
    }
  }, [examId]);

  React.useEffect(() => {
    void fetchGrid();
  }, [fetchGrid]);

  const canEdit = !!grid?.canEditResults && !grid?.isResultsFinalized;

  const updateCell = (
    studentId: string,
    examSubjectId: string,
    patch: Partial<CellValue>
  ): void => {
    const key: CellKey = `${studentId}:${examSubjectId}`;
    setCells((prev) => ({
      ...prev,
      [key]: { ...prev[key], ...patch } as CellValue,
    }));
  };

  const handleSaveAll = async (): Promise<void> => {
    if (!grid) return;
    setSaveError("");
    setSaveSuccess("");
    setSaveWarnings([]);

    const rows: ExamResultRowInput[] = [];
    for (const student of grid.students) {
      for (const col of grid.subjects) {
        const cell = cells[`${student.studentId}:${col.examSubjectId}`];
        if (!cell) continue;
        // Skip rows where nothing has been entered — the backend requires at
        // least one of marks/grade/absent per row, and a blank row is the
        // teacher saying "haven't graded this yet".
        const hasMarks = cell.marksObtained.trim() !== "";
        const hasGrade = cell.grade.trim() !== "";
        if (!hasMarks && !hasGrade && !cell.isAbsent) continue;

        const marksNum = hasMarks ? Number(cell.marksObtained) : null;
        if (hasMarks && !Number.isFinite(marksNum)) {
          setSaveError(
            `${student.name} — ${col.subject}: marks must be a number.`
          );
          return;
        }
        rows.push({
          studentId: student.studentId,
          examSubjectId: col.examSubjectId,
          marksObtained: marksNum,
          grade: hasGrade ? cell.grade.trim() : null,
          remarks: cell.remarks.trim() || null,
          isAbsent: cell.isAbsent,
        });
      }
    }

    if (rows.length === 0) {
      setSaveError("Enter at least one mark, grade, or absent marker before saving.");
      return;
    }

    const payload: UpsertExamResultsRequest = { rows };
    setIsSaving(true);
    try {
      const resp = await apiPut<UpsertExamResultsResponse>(
        `${API_ENDPOINTS.exams}/${examId}/results`,
        payload
      );
      setSaveSuccess(
        `Saved: ${resp.insertedCount} added, ${resp.updatedCount} updated${
          resp.skippedCount > 0 ? `, ${resp.skippedCount} skipped` : ""
        }.`
      );
      setSaveWarnings(resp.warnings ?? []);
      await fetchGrid();
    } catch (err) {
      setSaveError(err instanceof ApiError ? err.message : "Failed to save results.");
    } finally {
      setIsSaving(false);
    }
  };

  const handleUpload = async (
    e: React.ChangeEvent<HTMLInputElement>
  ): Promise<void> => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploadError("");
    setUploadSuccess("");
    setUploadWarnings([]);
    const formData = new FormData();
    formData.append("file", file);
    setIsUploading(true);
    try {
      const resp = await apiPostMultipart<UploadExamResultsCsvResponse>(
        `${API_ENDPOINTS.exams}/${examId}/results/upload`,
        formData
      );
      setUploadSuccess(
        `Uploaded: ${resp.inserted} added, ${resp.updated} updated${
          resp.skipped > 0 ? `, ${resp.skipped} skipped` : ""
        }.`
      );
      setUploadWarnings(resp.warnings ?? []);
      await fetchGrid();
    } catch (err) {
      setUploadError(err instanceof ApiError ? err.message : "Failed to upload CSV.");
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  const handleFinalize = async (): Promise<void> => {
    const confirmed = window.confirm(
      "Finalize results? Every student must have a mark, grade, or absent flag for every subject. Parents will be notified immediately."
    );
    if (!confirmed) return;
    setFinalizeError("");
    setFinalizeSuccess("");
    setIsFinalizing(true);
    try {
      const resp = await apiPut<FinalizeExamResultsResponse>(
        `${API_ENDPOINTS.exams}/${examId}/finalize-results`,
        {}
      );
      setFinalizeSuccess(
        `${resp.message} ${resp.studentCount} student${
          resp.studentCount === 1 ? "" : "s"
        }, ${resp.notifiedParentCount} parent${
          resp.notifiedParentCount === 1 ? "" : "s"
        } notified.`
      );
      await fetchGrid();
    } catch (err) {
      setFinalizeError(err instanceof ApiError ? err.message : "Failed to finalize.");
    } finally {
      setIsFinalizing(false);
    }
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

  if (error || !grid) {
    return (
      <PageShell>
        <ErrorState
          title="Could not load results"
          message={error || "Results not available."}
          onRetry={fetchGrid}
        />
      </PageShell>
    );
  }

  const classLabel = `${grid.className}${grid.section ? ` ${grid.section}` : ""}`;

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title={`Results — ${grid.examName}`}
        description={`${classLabel} · ${grid.students.length} student${
          grid.students.length === 1 ? "" : "s"
        } · ${grid.subjects.length} subject${grid.subjects.length === 1 ? "" : "s"}`}
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        backAction={
          <Link href={`/teacher/exams/${examId}`} prefetch={false}>
            <Button variant="ghost" size="sm">
              Back to exam
            </Button>
          </Link>
        }
        actions={
          canEdit ? (
            <div className="flex flex-wrap gap-2">
              <Button size="sm" onClick={handleSaveAll} disabled={isSaving}>
                {isSaving ? <Spinner size="sm" /> : "Save results"}
              </Button>
              <Button
                size="sm"
                variant="premium"
                onClick={handleFinalize}
                disabled={isFinalizing || isSaving}
              >
                {isFinalizing ? <Spinner size="sm" /> : "Finalize & notify"}
              </Button>
            </div>
          ) : undefined
        }
        stats={[
          {
            label: "Status",
            value: grid.isResultsFinalized ? "Finalized" : "Draft",
          },
          { label: "Students", value: grid.students.length.toString() },
        ]}
      />

      {grid.isResultsFinalized ? (
        <StatusBanner variant="success">
          Results are finalized and visible to parents. Further edits are locked.
        </StatusBanner>
      ) : null}

      {saveError ? <StatusBanner variant="error">{saveError}</StatusBanner> : null}
      {saveSuccess ? <StatusBanner variant="success">{saveSuccess}</StatusBanner> : null}
      {saveWarnings.length > 0 ? (
        <PageSection>
          <h4 className="mb-2 text-sm font-semibold">Save warnings</h4>
          <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
            {saveWarnings.map((w, i) => (
              <li key={i}>{w}</li>
            ))}
          </ul>
        </PageSection>
      ) : null}

      {finalizeError ? (
        <StatusBanner variant="error">{finalizeError}</StatusBanner>
      ) : null}
      {finalizeSuccess ? (
        <StatusBanner variant="success">{finalizeSuccess}</StatusBanner>
      ) : null}

      {canEdit ? (
        <PageSection className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 className="text-lg font-semibold">Upload CSV</h3>
              <p className="text-sm text-muted-foreground">
                Columns: <code>roll_number, subject, marks_obtained, grade, is_absent, remarks</code>.
                The CSV goes through the same validation as manual entry.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <input
                ref={fileInputRef}
                type="file"
                accept=".csv,text/csv"
                onChange={handleUpload}
                disabled={isUploading}
                className="hidden"
                id="csv-upload"
              />
              <Button
                size="sm"
                variant="outline"
                onClick={() => fileInputRef.current?.click()}
                disabled={isUploading}
              >
                {isUploading ? (
                  <Spinner size="sm" />
                ) : (
                  <>
                    <FileUp className="h-4 w-4" />
                    Choose CSV
                  </>
                )}
              </Button>
            </div>
          </div>
          {uploadError ? (
            <StatusBanner variant="error">{uploadError}</StatusBanner>
          ) : null}
          {uploadSuccess ? (
            <StatusBanner variant="success">{uploadSuccess}</StatusBanner>
          ) : null}
          {uploadWarnings.length > 0 ? (
            <div>
              <h4 className="mb-2 text-sm font-semibold">Upload warnings</h4>
              <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                {uploadWarnings.map((w, i) => (
                  <li key={i}>{w}</li>
                ))}
              </ul>
            </div>
          ) : null}
        </PageSection>
      ) : null}

      <PageSection>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] border-separate border-spacing-y-2 text-sm">
            <thead>
              <tr className="text-left text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                <th className="sticky left-0 z-10 bg-card/95 px-3 py-2">Student</th>
                {grid.subjects.map((col) => (
                  <th key={col.examSubjectId} className="px-3 py-2">
                    <div>{col.subject}</div>
                    <div className="text-[11px] font-normal normal-case text-muted-foreground">
                      Max {col.maxMarks}
                    </div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {grid.students.map((student) => (
                <tr key={student.studentId} className="align-top">
                  <td className="sticky left-0 z-10 rounded-l-lg bg-muted/30 px-3 py-3">
                    <div className="font-medium">{student.name}</div>
                    <div className="text-xs text-muted-foreground">
                      Roll {student.rollNumber}
                    </div>
                  </td>
                  {grid.subjects.map((col) => {
                    const key: CellKey = `${student.studentId}:${col.examSubjectId}`;
                    const cell =
                      cells[key] ??
                      ({
                        marksObtained: "",
                        grade: "",
                        remarks: "",
                        isAbsent: false,
                      } as CellValue);
                    return (
                      <td key={col.examSubjectId} className="bg-muted/30 px-2 py-2">
                        {canEdit ? (
                          <div className="space-y-1">
                            <Input
                              id={`marks-${key}`}
                              type="number"
                              min={0}
                              max={col.maxMarks}
                              step="0.5"
                              value={cell.marksObtained}
                              onChange={(e) =>
                                updateCell(student.studentId, col.examSubjectId, {
                                  marksObtained: e.target.value,
                                  isAbsent: false,
                                })
                              }
                              placeholder="Marks"
                              disabled={cell.isAbsent || isSaving}
                            />
                            <Input
                              id={`grade-${key}`}
                              value={cell.grade}
                              onChange={(e) =>
                                updateCell(student.studentId, col.examSubjectId, {
                                  grade: e.target.value,
                                })
                              }
                              placeholder="Grade"
                              disabled={isSaving}
                            />
                            <label className="flex items-center gap-2 text-xs text-muted-foreground">
                              <input
                                type="checkbox"
                                checked={cell.isAbsent}
                                onChange={(e) =>
                                  updateCell(student.studentId, col.examSubjectId, {
                                    isAbsent: e.target.checked,
                                    marksObtained: e.target.checked
                                      ? ""
                                      : cell.marksObtained,
                                  })
                                }
                                disabled={isSaving}
                              />
                              Absent
                            </label>
                          </div>
                        ) : (
                          <div className="space-y-1">
                            <div>
                              {cell.isAbsent ? (
                                <Badge variant="destructive">Absent</Badge>
                              ) : cell.marksObtained ? (
                                <span className="font-medium">
                                  {cell.marksObtained}
                                  <span className="text-muted-foreground">
                                    {" "}/ {col.maxMarks}
                                  </span>
                                </span>
                              ) : (
                                <span className="text-muted-foreground">—</span>
                              )}
                            </div>
                            {cell.grade ? (
                              <Badge variant="secondary">{cell.grade}</Badge>
                            ) : null}
                          </div>
                        )}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </PageSection>
    </PageShell>
  );
}
