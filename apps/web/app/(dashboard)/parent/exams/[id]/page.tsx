"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { useParentChildren } from "@/hooks/use-parent-children";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { GraduationCap } from "lucide-react";
import type { ExamDetail, ExamResultStudent } from "@/lib/types/exam";
import type { ParentChildItem } from "@/lib/types/student";

export default function ParentExamDetailPage(): React.ReactElement {
  const params = useParams<{ id: string }>();
  const examId = params?.id ?? "";

  const { children, isLoading: isLoadingChildren } = useParentChildren();

  const [exam, setExam] = React.useState<ExamDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  // Keyed by studentId — each linked child in this class gets a row of
  // per-subject marks.
  const [resultsByStudent, setResultsByStudent] = React.useState<
    Record<string, ExamResultStudent>
  >({});
  const [isLoadingResults, setIsLoadingResults] = React.useState(false);
  const [resultsError, setResultsError] = React.useState("");

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

  // Only children in *this exam's class* get result cards — a parent with a
  // kid in 5A and another in 7B shouldn't see a 7B row for a 5A exam.
  const childrenInExamClass: ParentChildItem[] = React.useMemo(() => {
    if (!exam) return [];
    return children.filter((c) => c.classId === exam.classId && c.isActive);
  }, [exam, children]);

  const fetchResults = React.useCallback(async () => {
    if (!exam || !exam.isResultsFinalized) return;
    if (childrenInExamClass.length === 0) return;

    setIsLoadingResults(true);
    setResultsError("");
    try {
      const entries = await Promise.all(
        childrenInExamClass.map(async (child) => {
          const r = await apiGet<ExamResultStudent>(
            `${API_ENDPOINTS.exams}/${exam.examId}/results/${child.id}`
          );
          return [child.id, r] as const;
        })
      );
      const map: Record<string, ExamResultStudent> = {};
      for (const [id, r] of entries) map[id] = r;
      setResultsByStudent(map);
    } catch (err) {
      setResultsError(
        err instanceof ApiError ? err.message : "Failed to load results."
      );
    } finally {
      setIsLoadingResults(false);
    }
  }, [exam, childrenInExamClass]);

  React.useEffect(() => {
    void fetchResults();
  }, [fetchResults]);

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

  if (isLoading || isLoadingChildren) {
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
        eyebrow="Family updates"
        title={exam.name}
        description={`${formatClassLabel(exam)} · ${exam.academicYear}`}
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        backAction={
          <Link href="/parent/exams" prefetch={false}>
            <Button variant="ghost" size="sm">
              Back to exams
            </Button>
          </Link>
        }
        stats={[
          {
            label: "Status",
            value: exam.isResultsFinalized ? "Results out" : "Upcoming",
          },
          { label: "Subjects", value: exam.subjects.length.toString() },
        ]}
      />

      <PageSection className="space-y-3">
        <h3 className="text-lg font-semibold">Schedule</h3>
        {exam.subjects.map((s) => (
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

      {exam.isResultsFinalized ? (
        <PageSection className="space-y-4">
          <div>
            <h3 className="text-lg font-semibold">Results</h3>
            <p className="text-sm text-muted-foreground">
              Published{" "}
              {exam.resultsFinalizedAt
                ? new Date(exam.resultsFinalizedAt).toLocaleString("en-IN", {
                    day: "numeric",
                    month: "short",
                    hour: "2-digit",
                    minute: "2-digit",
                  })
                : ""}
              .
            </p>
          </div>

          {isLoadingResults ? (
            <div className="flex min-h-48 items-center justify-center">
              <Spinner size="lg" />
            </div>
          ) : resultsError ? (
            <ErrorState
              title="Could not load results"
              message={resultsError}
              onRetry={fetchResults}
            />
          ) : childrenInExamClass.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No linked children for this class.
            </p>
          ) : (
            childrenInExamClass.map((child) => {
              const r = resultsByStudent[child.id];
              if (!r) return null;
              return (
                <Card key={child.id}>
                  <CardHeader className="pb-2">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div>
                        <CardTitle className="text-lg">{r.studentName}</CardTitle>
                        <p className="mt-1 text-sm text-muted-foreground">
                          Roll {r.rollNumber}
                        </p>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge variant="default">
                          {r.totalObtained} / {r.totalMax}
                        </Badge>
                        <Badge variant="secondary">
                          {r.percentage.toFixed(1)}%
                        </Badge>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="overflow-x-auto">
                      <table className="w-full min-w-[420px] text-sm">
                        <thead>
                          <tr className="text-left text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                            <th className="py-2">Subject</th>
                            <th className="py-2">Marks</th>
                            <th className="py-2">Grade</th>
                            <th className="py-2">Remarks</th>
                          </tr>
                        </thead>
                        <tbody>
                          {r.lines.map((line) => (
                            <tr
                              key={line.examSubjectId}
                              className="border-t border-border/60 align-top"
                            >
                              <td className="py-2 font-medium">{line.subject}</td>
                              <td className="py-2">
                                {line.isAbsent ? (
                                  <Badge variant="destructive">Absent</Badge>
                                ) : line.marksObtained !== null ? (
                                  <span>
                                    {line.marksObtained}
                                    <span className="text-muted-foreground">
                                      {" "}/ {line.maxMarks}
                                    </span>
                                  </span>
                                ) : (
                                  <span className="text-muted-foreground">—</span>
                                )}
                              </td>
                              <td className="py-2">
                                {line.grade ? (
                                  <Badge variant="secondary">{line.grade}</Badge>
                                ) : (
                                  <span className="text-muted-foreground">—</span>
                                )}
                              </td>
                              <td className="py-2 text-muted-foreground">
                                {line.remarks ?? "—"}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </CardContent>
                </Card>
              );
            })
          )}
        </PageSection>
      ) : null}
    </PageShell>
  );
}
