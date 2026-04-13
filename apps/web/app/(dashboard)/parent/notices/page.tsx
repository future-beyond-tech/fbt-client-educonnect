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
import { Bell } from "lucide-react";
import { AttachmentList } from "@/components/shared/attachment-list";

interface NoticeItem {
  noticeId: string;
  title: string;
  body: string;
  targetAudience: string;
  targetClassId: string | null;
  isPublished: boolean;
  publishedAt: string | null;
  expiresAt: string | null;
  createdAt: string;
}

export default function ParentNoticesPage(): React.ReactElement {
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
    fetchNotices();
  }, [fetchNotices]);

  const formatDate = (dateStr: string): string => {
    return new Date(dateStr).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Family updates"
        title="Notices"
        description="Catch every announcement, circular, and school-wide update in one place."
        icon={<Bell className="h-6 w-6" aria-hidden="true" />}
        stats={[{ label: "Active notices", value: notices.length.toString() }]}
      />

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchNotices} />
      ) : notices.length === 0 ? (
        <EmptyState
          title="No notices"
          description="There are no notices at this time."
          icon={<Bell className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <PageSection className="space-y-4">
          {notices.map((notice) => (
            <Card
              key={notice.noticeId}
              className="cursor-pointer transition-shadow hover:shadow-md"
              onClick={() =>
                setExpandedId(expandedId === notice.noticeId ? null : notice.noticeId)
              }
            >
              <CardHeader className="pb-2">
                <div className="flex items-start justify-between gap-2">
                  <CardTitle className="text-lg">{notice.title}</CardTitle>
                  <Badge variant={notice.targetAudience === "All" ? "default" : "secondary"}>
                    {notice.targetAudience}
                  </Badge>
                </div>
                {notice.publishedAt && (
                  <p className="text-xs text-muted-foreground">
                    {formatDate(notice.publishedAt)}
                  </p>
                )}
              </CardHeader>
              {expandedId === notice.noticeId && (
                <CardContent>
                  <p className="whitespace-pre-wrap text-sm">{notice.body}</p>
                  <div className="mt-3">
                    <AttachmentList entityId={notice.noticeId} entityType="notice" />
                  </div>
                  {notice.expiresAt && (
                    <p className="mt-3 text-xs text-muted-foreground">
                      Expires: {formatDate(notice.expiresAt)}
                    </p>
                  )}
                </CardContent>
              )}
            </Card>
          ))}
        </PageSection>
      )}
    </PageShell>
  );
}
