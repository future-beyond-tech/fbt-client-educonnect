"use client";

import * as React from "react";
import { apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { useParentChildren } from "@/hooks/use-parent-children";
import { formatNoticeAudienceDetails, formatNoticeAudienceLabel } from "@/lib/notice-targeting";
import type { NoticeItem } from "@/lib/types/notice";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { ParentChildFilter } from "@/components/shared/parent-child-filter";
import {
  PageHeader,
  PageSection,
  PageShell,
} from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { Bell, Paperclip } from "lucide-react";
import { AttachmentList } from "@/components/shared/attachment-list";

export default function ParentNoticesPage(): React.ReactElement {
  const {
    children,
    selectedChild,
    selectedChildId,
    hasMultipleChildren,
    error: childrenError,
    setSelectedChildId,
  } = useParentChildren();
  const [notices, setNotices] = React.useState<NoticeItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [expandedId, setExpandedId] = React.useState<string | null>(null);

  const fetchNotices = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<NoticeItem[]>(API_ENDPOINTS.notices);
      setNotices(data);
    } catch {
      setError("Failed to load notices.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void fetchNotices();
  }, [fetchNotices]);

  const visibleNotices = React.useMemo(() => {
    if (!selectedChild) {
      return notices;
    }

    return notices.filter((notice) => {
      if (notice.targetAudience === "All") {
        return true;
      }

      return notice.targetClasses.some(
        (targetClass) => targetClass.classId === selectedChild.classId
      );
    });
  }, [notices, selectedChild]);

  const formatDate = (dateStr: string): string => {
    return new Date(dateStr).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  const handleChildChange = (value: string): void => {
    setSelectedChildId(value);
  };

  const noticeAudienceForFamily = (notice: NoticeItem): string | null => {
    if (selectedChild) {
      return selectedChild.name;
    }

    if (notice.targetAudience === "All") {
      return children.map((child) => child.name).join(", ");
    }

    const targetClassIds = new Set(
      notice.targetClasses.map((targetClass) => targetClass.classId)
    );
    const names = children
      .filter((child) => targetClassIds.has(child.classId))
      .map((child) => child.name);

    return names.length > 0 ? names.join(", ") : null;
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Family updates"
        title="Notices"
        description={
          selectedChild
            ? `Catch every announcement, circular, and school-wide update for ${selectedChild.name}.`
            : "Catch every announcement, circular, and school-wide update for all linked children in one place."
        }
        icon={<Bell className="h-6 w-6" aria-hidden="true" />}
        stats={[{ label: "Active notices", value: visibleNotices.length.toString() }]}
      />

      {hasMultipleChildren || childrenError ? (
        <PageSection className="space-y-4">
          {hasMultipleChildren ? (
            <div className="max-w-md">
              <ParentChildFilter
                students={children}
                value={selectedChildId}
                onChange={handleChildChange}
                label="Showing notices for"
                className="bg-card/96 backdrop-blur-none"
              />
            </div>
          ) : null}
          {childrenError ? (
            <StatusBanner variant="error">
              Child filters are unavailable right now. Showing notices without
              family-specific labels.
            </StatusBanner>
          ) : null}
        </PageSection>
      ) : null}

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchNotices} />
      ) : visibleNotices.length === 0 ? (
        <EmptyState
          title="No notices"
          description={
            selectedChild
              ? `There are no notices for ${selectedChild.name} at this time.`
              : "There are no notices at this time."
          }
          icon={<Bell className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <PageSection className="space-y-4">
          {visibleNotices.map((notice) => {
            const familyAudienceLabel = noticeAudienceForFamily(notice);

            return (
              <Card
                key={notice.noticeId}
                className="cursor-pointer border-l-4 border-l-[rgb(var(--primary))] transition-[box-shadow,border-color] hover:border-l-[rgb(var(--primary-strong))] hover:shadow-[0_8px_30px_-8px_rgb(var(--primary)/0.25)]"
                onClick={() =>
                  setExpandedId(expandedId === notice.noticeId ? null : notice.noticeId)
                }
              >
                <CardHeader className="pb-2">
                  <div className="flex items-start justify-between gap-2">
                    <CardTitle className="text-lg">{notice.title}</CardTitle>
                    <div className="flex flex-wrap items-center justify-end gap-2">
                      {notice.attachmentCount > 0 && (
                        <Badge
                          variant="outline"
                          className="gap-1 border-[rgb(var(--muted-foreground)/0.25)] bg-[rgb(var(--muted))] text-[rgb(var(--muted-foreground))]"
                          aria-label={`${notice.attachmentCount} attachment${notice.attachmentCount === 1 ? "" : "s"}`}
                        >
                          <Paperclip className="h-3 w-3" aria-hidden="true" />
                          {notice.attachmentCount}
                        </Badge>
                      )}
                      <Badge
                        variant="outline"
                        className="border-[rgb(var(--success)/0.3)] bg-[rgb(var(--success)/0.15)] text-[rgb(var(--success))]"
                      >
                        {formatNoticeAudienceLabel(notice)}
                      </Badge>
                    </div>
                  </div>
                  {notice.publishedAt && (
                    <p className="text-xs text-muted-foreground">
                      {formatDate(notice.publishedAt)}
                    </p>
                  )}
                  {familyAudienceLabel ? (
                    <p className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                      For {familyAudienceLabel}
                    </p>
                  ) : null}
                </CardHeader>
                {expandedId === notice.noticeId && (
                  <CardContent>
                    <p className="whitespace-pre-wrap text-sm">{notice.body}</p>
                    {formatNoticeAudienceDetails(notice) && (
                      <p className="mt-3 text-xs text-muted-foreground">
                        {formatNoticeAudienceDetails(notice)}
                      </p>
                    )}
                    <div className="mt-3">
                      <AttachmentList
                        entityId={notice.noticeId}
                        entityType="notice"
                        publishedAt={notice.publishedAt}
                      />
                    </div>
                    {notice.expiresAt && (
                      <p className="mt-3 text-xs text-muted-foreground">
                        Expires: {formatDate(notice.expiresAt)}
                      </p>
                    )}
                  </CardContent>
                )}
              </Card>
            );
          })}
        </PageSection>
      )}
    </PageShell>
  );
}
