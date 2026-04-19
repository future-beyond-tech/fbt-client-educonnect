"use client";

import * as React from "react";
import { apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { useParentChildren } from "@/hooks/use-parent-children";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { ParentChildFilter } from "@/components/shared/parent-child-filter";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { BookOpen } from "lucide-react";
import { AttachmentList } from "@/components/shared/attachment-list";

interface HomeworkItem {
  homeworkId: string;
  classId: string;
  /** Denormalised from the API so parents see "5 A · Science" on each card. */
  className: string;
  section: string;
  subject: string;
  title: string;
  description: string;
  dueDate: string;
  isEditable: boolean;
  publishedAt: string;
}

export default function ParentHomeworkPage(): React.ReactElement {
  const {
    children,
    selectedChild,
    selectedChildId,
    hasMultipleChildren,
    error: childrenError,
    setSelectedChildId,
  } = useParentChildren();
  const [homework, setHomework] = React.useState<HomeworkItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [subjectFilter, setSubjectFilter] = React.useState("");
  const childNamesByClassId = React.useMemo(() => {
    const next = new Map<string, string[]>();

    children.forEach((child) => {
      const classNames = next.get(child.classId) ?? [];
      classNames.push(child.name);
      next.set(child.classId, classNames);
    });

    return next;
  }, [children]);

  const fetchHomework = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (selectedChild) {
        params.set("classId", selectedChild.classId);
      }
      if (subjectFilter) {
        params.set("subject", subjectFilter);
      }

      const data = await apiGet<HomeworkItem[]>(
        params.size > 0
          ? `${API_ENDPOINTS.homework}?${params.toString()}`
          : API_ENDPOINTS.homework
      );
      setHomework(data);
    } catch {
      setError("Failed to load homework.");
    } finally {
      setIsLoading(false);
    }
  }, [selectedChild, subjectFilter]);

  React.useEffect(() => {
    void fetchHomework();
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

  const handleChildChange = (value: string): void => {
    setSelectedChildId(value);
    setSubjectFilter("");
  };

  const homeworkAudienceLabel = (item: HomeworkItem): string | null => {
    if (selectedChild) {
      return selectedChild.name;
    }

    const childNames = childNamesByClassId.get(item.classId) ?? [];
    return childNames.length > 0 ? childNames.join(", ") : null;
  };

  // Format class + section as "5 A". Handles the defensive case where the
  // API fails to populate either field (shouldn't happen under current data
  // rules, but we still render "" gracefully rather than crashing).
  const formatClassLabel = (item: HomeworkItem): string => {
    const name = (item.className ?? "").trim();
    const section = (item.section ?? "").trim();
    if (!name && !section) return "";
    return section ? `${name} ${section}` : name;
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Family updates"
        title="Homework"
        description={
          selectedChild
            ? `Keep track of assignments, due dates, and subject-wise work for ${selectedChild.name}.`
            : "Keep track of assignments, due dates, and subject-wise work for all linked children."
        }
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        stats={[
          { label: "Assignments", value: homework.length.toString() },
          { label: "Subjects", value: subjects.length.toString() },
        ]}
      />

      {hasMultipleChildren || childrenError ? (
        <PageSection className="space-y-4">
          {hasMultipleChildren ? (
            <div className="max-w-md">
              <ParentChildFilter
                students={children}
                value={selectedChildId}
                onChange={handleChildChange}
                label="Showing homework for"
                className="bg-card/96 backdrop-blur-none"
              />
            </div>
          ) : null}
          {childrenError ? (
            <StatusBanner variant="error">
              Child filters are unavailable right now. Showing the homework list
              without child-specific labels.
            </StatusBanner>
          ) : null}
        </PageSection>
      ) : null}

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
          description={
            selectedChild
              ? `No homework assignments found for ${selectedChild.name}.`
              : "No homework assignments found."
          }
          icon={<BookOpen className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <PageSection className="space-y-4">
          {homework.map((item) => {
            const audienceLabel = homeworkAudienceLabel(item);

            return (
              <Card key={item.homeworkId}>
                <CardHeader className="pb-2">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <CardTitle className="text-lg">{item.title}</CardTitle>
                      {/* Class + subject on a dedicated secondary line so
                          parents with children in multiple classes can tell
                          at a glance which class this homework belongs to. */}
                      {formatClassLabel(item) && (
                        <p className="mt-1 text-sm text-muted-foreground">
                          {formatClassLabel(item)} · {item.subject}
                        </p>
                      )}
                    </div>
                    <Badge variant="secondary">{item.subject}</Badge>
                  </div>
                </CardHeader>
                <CardContent>
                  {audienceLabel ? (
                    <p className="mb-3 text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                      For {audienceLabel}
                    </p>
                  ) : null}
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
            );
          })}
        </PageSection>
      )}
    </PageShell>
  );
}
