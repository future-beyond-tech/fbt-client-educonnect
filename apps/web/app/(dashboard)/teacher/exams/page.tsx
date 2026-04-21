"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { GraduationCap, Plus } from "lucide-react";
import type { ExamListItem } from "@/lib/types/exam";
import type { TeacherClassItem } from "@/lib/types/teacher";

export default function TeacherExamsPage(): React.ReactElement {
  const router = useRouter();
  const [exams, setExams] = React.useState<ExamListItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const [assignments, setAssignments] = React.useState<TeacherClassItem[]>([]);
  const [assignmentError, setAssignmentError] = React.useState("");

  const [classFilter, setClassFilter] = React.useState("");

  const fetchAssignments = React.useCallback(async () => {
    setAssignmentError("");
    try {
      const data = await apiGet<TeacherClassItem[]>(API_ENDPOINTS.teachersMyClasses);
      setAssignments(data);
    } catch (err) {
      setAssignmentError(err instanceof ApiError ? err.message : "Failed to load assignments.");
    }
  }, []);

  const fetchExams = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (classFilter) params.set("classId", classFilter);
      const qs = params.toString();
      const url = qs ? `${API_ENDPOINTS.exams}?${qs}` : API_ENDPOINTS.exams;
      const data = await apiGet<ExamListItem[]>(url);
      setExams(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load exams.");
    } finally {
      setIsLoading(false);
    }
  }, [classFilter]);

  React.useEffect(() => {
    void fetchAssignments();
  }, [fetchAssignments]);

  React.useEffect(() => {
    void fetchExams();
  }, [fetchExams]);

  const classesForTeacher = React.useMemo(() => {
    const map = new Map<
      string,
      { classId: string; className: string; section: string; isClassTeacher: boolean }
    >();
    for (const a of assignments) {
      const existing = map.get(a.classId);
      if (!existing) {
        map.set(a.classId, {
          classId: a.classId,
          className: a.className,
          section: a.section,
          isClassTeacher: a.isClassTeacher,
        });
      } else if (a.isClassTeacher) {
        existing.isClassTeacher = true;
      }
    }
    return Array.from(map.values()).sort((a, b) => {
      const nameCompare = a.className.localeCompare(b.className);
      if (nameCompare !== 0) return nameCompare;
      return a.section.localeCompare(b.section);
    });
  }, [assignments]);

  const hasAnyClassTeacherAssignment = classesForTeacher.some((c) => c.isClassTeacher);

  const formatClassLabel = (c: { className: string; section: string }): string => {
    const name = (c.className ?? "").trim();
    const section = (c.section ?? "").trim();
    if (!name && !section) return "";
    return section ? `${name} ${section}` : name;
  };

  const formatDate = (dateStr: string | null): string => {
    if (!dateStr) return "—";
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  const getStatusBadge = (exam: ExamListItem): React.ReactElement => {
    if (exam.isResultsFinalized) {
      return <Badge variant="default">Results finalized</Badge>;
    }
    if (exam.isSchedulePublished) {
      return <Badge variant="outline">Schedule published</Badge>;
    }
    return <Badge variant="secondary">Draft</Badge>;
  };

  const publishedCount = exams.filter((e) => e.isSchedulePublished).length;
  const finalizedCount = exams.filter((e) => e.isResultsFinalized).length;

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title="Exams"
        description="Plan exam schedules, publish them to parents, then record and finalize results."
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        actions={
          hasAnyClassTeacherAssignment ? (
            <Link href="/teacher/exams/new" prefetch={false}>
              <Button size="sm" variant="premium">
                <Plus className="h-4 w-4" />
                New exam
              </Button>
            </Link>
          ) : undefined
        }
        stats={[
          { label: "Total", value: exams.length.toString() },
          { label: "Published", value: publishedCount.toString() },
          { label: "Finalized", value: finalizedCount.toString() },
        ]}
      />

      {assignmentError ? (
        <ErrorState
          title="Could not load classes"
          message={assignmentError}
          onRetry={fetchAssignments}
        />
      ) : null}

      {!hasAnyClassTeacherAssignment && !assignmentError ? (
        <PageSection>
          <p className="text-sm text-muted-foreground">
            Only the class teacher can create or publish exams. Ask your administrator
            to mark you as a class teacher for at least one class if you need to plan
            exams.
          </p>
        </PageSection>
      ) : null}

      {classesForTeacher.length > 1 ? (
        <PageSection>
          <div className="grid gap-3 sm:max-w-sm">
            <Select
              label="Filter by class"
              value={classFilter}
              onChange={(e) => setClassFilter(e.target.value)}
            >
              <option value="">All classes</option>
              {classesForTeacher.map((c) => (
                <option key={c.classId} value={c.classId}>
                  {formatClassLabel(c)}
                </option>
              ))}
            </Select>
          </div>
        </PageSection>
      ) : null}

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchExams} />
      ) : exams.length === 0 ? (
        <EmptyState
          title="No exams yet"
          description={
            hasAnyClassTeacherAssignment
              ? "Create your first exam to publish the schedule to parents."
              : "You haven't been assigned as a class teacher yet."
          }
          icon={<GraduationCap className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
          action={
            hasAnyClassTeacherAssignment
              ? {
                  label: "Create exam",
                  onClick: () => router.push("/teacher/exams/new"),
                }
              : undefined
          }
        />
      ) : (
        <PageSection className="space-y-3">
          {exams.map((exam) => (
            <Card key={exam.examId}>
              <CardHeader className="pb-2">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <CardTitle className="text-lg">{exam.name}</CardTitle>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {formatClassLabel(exam)} · {exam.academicYear}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {getStatusBadge(exam)}
                    <Badge variant="secondary">
                      {exam.subjectCount} subject{exam.subjectCount === 1 ? "" : "s"}
                    </Badge>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-muted-foreground">
                  <span>
                    <span className="font-medium text-foreground">From:</span>{" "}
                    {formatDate(exam.firstExamDate)}
                  </span>
                  <span>
                    <span className="font-medium text-foreground">To:</span>{" "}
                    {formatDate(exam.lastExamDate)}
                  </span>
                  {exam.schedulePublishedAt ? (
                    <span>
                      Schedule published{" "}
                      {new Date(exam.schedulePublishedAt).toLocaleDateString("en-IN", {
                        day: "numeric",
                        month: "short",
                      })}
                    </span>
                  ) : null}
                  {exam.resultsFinalizedAt ? (
                    <span>
                      Results finalized{" "}
                      {new Date(exam.resultsFinalizedAt).toLocaleDateString("en-IN", {
                        day: "numeric",
                        month: "short",
                      })}
                    </span>
                  ) : null}
                </div>

                <div className="mt-4 flex flex-wrap gap-2">
                  <Link href={`/teacher/exams/${exam.examId}`} prefetch={false}>
                    <Button size="sm" variant="outline">
                      {exam.isSchedulePublished ? "View schedule" : "Review & publish"}
                    </Button>
                  </Link>
                  {exam.isSchedulePublished ? (
                    <Link
                      href={`/teacher/exams/${exam.examId}/results`}
                      prefetch={false}
                    >
                      <Button size="sm">
                        {exam.isResultsFinalized ? "View results" : "Enter results"}
                      </Button>
                    </Link>
                  ) : null}
                </div>
              </CardContent>
            </Card>
          ))}
        </PageSection>
      )}
    </PageShell>
  );
}
