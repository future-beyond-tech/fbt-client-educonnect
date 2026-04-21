"use client";

import * as React from "react";
import Link from "next/link";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { useParentChildren } from "@/hooks/use-parent-children";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { ParentChildFilter } from "@/components/shared/parent-child-filter";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { GraduationCap } from "lucide-react";
import type { ExamListItem } from "@/lib/types/exam";

export default function ParentExamsPage(): React.ReactElement {
  const {
    children,
    selectedChild,
    selectedChildId,
    hasMultipleChildren,
    error: childrenError,
    setSelectedChildId,
  } = useParentChildren();

  const [exams, setExams] = React.useState<ExamListItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const childNamesByClassId = React.useMemo(() => {
    const next = new Map<string, string[]>();
    children.forEach((child) => {
      const names = next.get(child.classId) ?? [];
      names.push(child.name);
      next.set(child.classId, names);
    });
    return next;
  }, [children]);

  const fetchExams = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (selectedChild) params.set("classId", selectedChild.classId);
      const qs = params.toString();
      const url = qs ? `${API_ENDPOINTS.exams}?${qs}` : API_ENDPOINTS.exams;
      const data = await apiGet<ExamListItem[]>(url);
      setExams(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load exams.");
    } finally {
      setIsLoading(false);
    }
  }, [selectedChild]);

  React.useEffect(() => {
    void fetchExams();
  }, [fetchExams]);

  const formatClassLabel = (e: ExamListItem): string => {
    const name = (e.className ?? "").trim();
    const section = (e.section ?? "").trim();
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

  const audienceLabel = (exam: ExamListItem): string | null => {
    if (selectedChild) return selectedChild.name;
    const names = childNamesByClassId.get(exam.classId) ?? [];
    return names.length > 0 ? names.join(", ") : null;
  };

  const upcomingCount = exams.filter((e) => !e.isResultsFinalized).length;
  const resultsOutCount = exams.filter((e) => e.isResultsFinalized).length;

  return (
    <PageShell>
      <PageHeader
        eyebrow="Family updates"
        title="Exams"
        description={
          selectedChild
            ? `Upcoming schedules and released results for ${selectedChild.name}.`
            : "Upcoming schedules and released results for all linked children."
        }
        icon={<GraduationCap className="h-6 w-6" aria-hidden="true" />}
        stats={[
          { label: "Total", value: exams.length.toString() },
          { label: "Upcoming", value: upcomingCount.toString() },
          { label: "Results out", value: resultsOutCount.toString() },
        ]}
      />

      {hasMultipleChildren || childrenError ? (
        <PageSection className="space-y-4">
          {hasMultipleChildren ? (
            <div className="max-w-md">
              <ParentChildFilter
                students={children}
                value={selectedChildId}
                onChange={setSelectedChildId}
                label="Showing exams for"
                className="bg-card/96 backdrop-blur-none"
              />
            </div>
          ) : null}
          {childrenError ? (
            <StatusBanner variant="error">
              Child filters are unavailable right now. Showing the exam list without
              child-specific labels.
            </StatusBanner>
          ) : null}
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
            selectedChild
              ? `No exam schedules have been published for ${selectedChild.name} yet.`
              : "No exam schedules have been published yet."
          }
          icon={<GraduationCap className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <PageSection className="space-y-3">
          {exams.map((exam) => {
            const audience = audienceLabel(exam);
            return (
              <Card key={exam.examId}>
                <CardHeader className="pb-2">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <CardTitle className="text-lg">{exam.name}</CardTitle>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {formatClassLabel(exam)} · {exam.academicYear}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      {exam.isResultsFinalized ? (
                        <Badge variant="default">Results out</Badge>
                      ) : (
                        <Badge variant="outline">Upcoming</Badge>
                      )}
                      <Badge variant="secondary">
                        {exam.subjectCount} subject{exam.subjectCount === 1 ? "" : "s"}
                      </Badge>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  {audience ? (
                    <p className="mb-3 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                      For {audience}
                    </p>
                  ) : null}
                  <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-muted-foreground">
                    <span>
                      <span className="font-medium text-foreground">From:</span>{" "}
                      {formatDate(exam.firstExamDate)}
                    </span>
                    <span>
                      <span className="font-medium text-foreground">To:</span>{" "}
                      {formatDate(exam.lastExamDate)}
                    </span>
                  </div>
                  <div className="mt-4">
                    <Link href={`/parent/exams/${exam.examId}`} prefetch={false}>
                      <Button size="sm" variant="outline">
                        {exam.isResultsFinalized ? "View results" : "View schedule"}
                      </Button>
                    </Link>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </PageSection>
      )}
    </PageShell>
  );
}
