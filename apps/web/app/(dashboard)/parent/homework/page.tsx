"use client";

import * as React from "react";
import { apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { BookOpen } from "lucide-react";
import { AttachmentList } from "@/components/shared/attachment-list";

interface HomeworkItem {
  homeworkId: string;
  classId: string;
  subject: string;
  title: string;
  description: string;
  dueDate: string;
  isEditable: boolean;
  publishedAt: string;
}

export default function ParentHomeworkPage(): React.ReactElement {
  const [homework, setHomework] = React.useState<HomeworkItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [subjectFilter, setSubjectFilter] = React.useState("");

  const fetchHomework = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = subjectFilter ? `?subject=${encodeURIComponent(subjectFilter)}` : "";
      const data = await apiGet<HomeworkItem[]>(`${API_ENDPOINTS.homework}${params}`);
      setHomework(data);
    } catch {
      setError("Failed to load homework.");
    } finally {
      setIsLoading(false);
    }
  }, [subjectFilter]);

  React.useEffect(() => {
    fetchHomework();
  }, [fetchHomework]);

  const subjects = React.useMemo(() => {
    const unique = new Set(homework.map((h) => h.subject));
    return Array.from(unique).sort();
  }, [homework]);

  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
    });
  };

  const formatDateTime = (dateStr: string): string => {
    const date = new Date(dateStr);
    return date.toLocaleString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const isOverdue = (dueDate: string): boolean => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return new Date(dueDate + "T00:00:00") < today;
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Family updates"
        title="Homework"
        description="Keep track of assignments, due dates, and subject-wise work for your child."
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        stats={[
          { label: "Assignments", value: homework.length.toString() },
          { label: "Subjects", value: subjects.length.toString() },
        ]}
      />

      {subjects.length > 0 && (
        <PageSection className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setSubjectFilter("")}
            className={`rounded-full px-4 py-2 text-sm font-semibold transition-colors ${
              !subjectFilter
                ? "bg-primary text-primary-foreground shadow-[0_16px_32px_-22px_rgba(15,40,69,0.55)]"
                : "bg-secondary text-secondary-foreground hover:bg-secondary/80"
            }`}
          >
            All subjects
          </button>
          {subjects.map((subject) => (
            <button
              key={subject}
              type="button"
              onClick={() => setSubjectFilter(subject)}
              className={`rounded-full px-4 py-2 text-sm font-semibold transition-colors ${
                subjectFilter === subject
                  ? "bg-primary text-primary-foreground shadow-[0_16px_32px_-22px_rgba(15,40,69,0.55)]"
                  : "bg-secondary text-secondary-foreground hover:bg-secondary/80"
              }`}
            >
              {subject}
            </button>
          ))}
        </PageSection>
      )}

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchHomework} />
      ) : homework.length === 0 ? (
        <EmptyState
          title="No homework"
          description="No homework assignments found."
          icon={<BookOpen className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <PageSection className="space-y-4">
          {homework.map((item) => (
            <Card key={item.homeworkId}>
              <CardHeader className="pb-2">
                <div className="flex items-start justify-between gap-2">
                  <CardTitle className="text-lg">{item.title}</CardTitle>
                  <Badge variant="secondary">{item.subject}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <p className="mb-3 text-sm text-muted-foreground">
                  {item.description}
                </p>
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="text-muted-foreground">Due:</span>
                  <span className={isOverdue(item.dueDate) ? "font-medium text-destructive" : "font-medium"}>
                    {formatDate(item.dueDate)}
                  </span>
                  {isOverdue(item.dueDate) && (
                    <Badge variant="destructive">Overdue</Badge>
                  )}
                </div>
                <div className="mt-2 text-sm text-muted-foreground">
                  Published {formatDateTime(item.publishedAt)}
                </div>
                <div className="mt-3">
                  <AttachmentList entityId={item.homeworkId} entityType="homework" />
                </div>
              </CardContent>
            </Card>
          ))}
        </PageSection>
      )}
    </PageShell>
  );
}
