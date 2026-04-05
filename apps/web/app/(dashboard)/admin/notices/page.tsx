"use client";

import * as React from "react";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { Bell, Plus, Send } from "lucide-react";
import { AttachmentUploader, type UploadedFile } from "@/components/shared/attachment-uploader";
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

interface CreateNoticeResponse {
  noticeId: string;
  message: string;
}

interface PublishNoticeResponse {
  message: string;
}

export default function AdminNoticesPage(): React.ReactElement {
  const [notices, setNotices] = React.useState<NoticeItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  // Create form state
  const [showCreateForm, setShowCreateForm] = React.useState(false);
  const [createTitle, setCreateTitle] = React.useState("");
  const [createBody, setCreateBody] = React.useState("");
  const [createTargetAudience, setCreateTargetAudience] = React.useState("All");
  const [createTargetClassId, setCreateTargetClassId] = React.useState("");
  const [createExpiresAt, setCreateExpiresAt] = React.useState("");
  const [createError, setCreateError] = React.useState("");
  const [isCreating, setIsCreating] = React.useState(false);

  const [isPublishing, setIsPublishing] = React.useState<string | null>(null);
  const [successMessage, setSuccessMessage] = React.useState("");

  // Post-create attachment flow
  const [newNoticeId, setNewNoticeId] = React.useState<string | null>(null);
  const [newNoticeAttachments, setNewNoticeAttachments] = React.useState<UploadedFile[]>([]);

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

  const handleCreate = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setCreateError("");
    setSuccessMessage("");

    if (!createTitle || !createBody) {
      setCreateError("Title and body are required.");
      return;
    }

    if ((createTargetAudience === "Class" || createTargetAudience === "Section") && !createTargetClassId) {
      setCreateError("Class ID is required for class/section targeting.");
      return;
    }

    setIsCreating(true);
    try {
      const response = await apiPost<CreateNoticeResponse>(API_ENDPOINTS.notices, {
        title: createTitle,
        body: createBody,
        targetAudience: createTargetAudience,
        targetClassId: createTargetAudience !== "All" ? createTargetClassId : null,
        expiresAt: createExpiresAt || null,
      });
      setSuccessMessage(response.message);
      setShowCreateForm(false);
      setCreateTitle("");
      setCreateBody("");
      setCreateTargetAudience("All");
      setCreateTargetClassId("");
      setCreateExpiresAt("");
      // Show attachment uploader for newly created draft notice
      setNewNoticeId(response.noticeId);
      setNewNoticeAttachments([]);
      fetchNotices();
    } catch (err) {
      setCreateError(err instanceof ApiError ? err.message : "Failed to create notice.");
    } finally {
      setIsCreating(false);
    }
  };

  const handlePublish = async (noticeId: string): Promise<void> => {
    setSuccessMessage("");
    setIsPublishing(noticeId);
    try {
      const response = await apiPut<PublishNoticeResponse>(
        `${API_ENDPOINTS.notices}/${noticeId}/publish`,
        { noticeId }
      );
      setSuccessMessage(response.message);
      fetchNotices();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to publish notice.");
    } finally {
      setIsPublishing(null);
    }
  };

  const formatDate = (dateStr: string): string => {
    return new Date(dateStr).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const drafts = notices.filter((n) => !n.isPublished);
  const published = notices.filter((n) => n.isPublished);

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Notices</h1>
          <p className="text-muted-foreground">
            Create and manage school notices.
          </p>
        </div>
        <Button
          onClick={() => {
            setShowCreateForm(!showCreateForm);
            setCreateError("");
            setSuccessMessage("");
          }}
          size="sm"
        >
          <Plus className="mr-1 h-4 w-4" />
          New Notice
        </Button>
      </div>

      {successMessage && (
        <div className="rounded-md bg-green-50 p-3 text-sm text-green-800 dark:bg-green-950 dark:text-green-200">
          {successMessage}
        </div>
      )}

      {newNoticeId && (
        <Card>
          <CardContent className="space-y-3 p-4">
            <h3 className="font-semibold">Attach Files to Notice</h3>
            <p className="text-sm text-muted-foreground">
              Optionally attach images or PDFs to the draft notice before publishing.
            </p>
            <AttachmentUploader
              entityId={newNoticeId}
              entityType="notice"
              existingAttachments={newNoticeAttachments}
              onAttachmentsChange={setNewNoticeAttachments}
            />
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => {
                setNewNoticeId(null);
                setNewNoticeAttachments([]);
                fetchNotices();
              }}
            >
              Done
            </Button>
          </CardContent>
        </Card>
      )}

      {showCreateForm && (
        <Card>
          <CardContent className="p-4">
            <form onSubmit={handleCreate} className="space-y-3">
              <h3 className="font-semibold">Create Notice</h3>
              <div className="space-y-1">
                <label htmlFor="createTitle" className="text-sm font-medium">
                  Title
                </label>
                <Input
                  id="createTitle"
                  placeholder="Notice title"
                  value={createTitle}
                  onChange={(e) => setCreateTitle(e.target.value)}
                  disabled={isCreating}
                />
              </div>
              <div className="space-y-1">
                <label htmlFor="createBody" className="text-sm font-medium">
                  Body
                </label>
                <textarea
                  id="createBody"
                  placeholder="Notice content..."
                  value={createBody}
                  onChange={(e) => setCreateBody(e.target.value)}
                  disabled={isCreating}
                  rows={4}
                  className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <label htmlFor="createTargetAudience" className="text-sm font-medium">
                    Target Audience
                  </label>
                  <select
                    id="createTargetAudience"
                    value={createTargetAudience}
                    onChange={(e) => setCreateTargetAudience(e.target.value)}
                    disabled={isCreating}
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  >
                    <option value="All">All</option>
                    <option value="Class">Class</option>
                    <option value="Section">Section</option>
                  </select>
                </div>
                {createTargetAudience !== "All" && (
                  <div className="space-y-1">
                    <label htmlFor="createTargetClassId" className="text-sm font-medium">
                      Class ID
                    </label>
                    <Input
                      id="createTargetClassId"
                      placeholder="Enter class ID"
                      value={createTargetClassId}
                      onChange={(e) => setCreateTargetClassId(e.target.value)}
                      disabled={isCreating}
                    />
                  </div>
                )}
              </div>
              <div className="space-y-1">
                <label htmlFor="createExpiresAt" className="text-sm font-medium">
                  Expires At (optional)
                </label>
                <Input
                  id="createExpiresAt"
                  type="datetime-local"
                  value={createExpiresAt}
                  onChange={(e) => setCreateExpiresAt(e.target.value)}
                  disabled={isCreating}
                />
              </div>
              {createError && (
                <p className="text-sm text-destructive">{createError}</p>
              )}
              <div className="flex gap-2">
                <Button type="submit" size="sm" disabled={isCreating}>
                  {isCreating ? <Spinner size="sm" /> : "Create Draft"}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setShowCreateForm(false)}
                  disabled={isCreating}
                >
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchNotices} />
      ) : notices.length === 0 ? (
        <EmptyState
          title="No notices"
          description="Create your first school notice."
          icon={<Bell className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
          action={{
            label: "Create Notice",
            onClick: () => setShowCreateForm(true),
          }}
        />
      ) : (
        <div className="space-y-6">
          {drafts.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-lg font-semibold">Drafts</h2>
              {drafts.map((notice) => (
                <Card key={notice.noticeId}>
                  <CardHeader className="pb-2">
                    <div className="flex items-start justify-between gap-2">
                      <CardTitle className="text-lg">{notice.title}</CardTitle>
                      <div className="flex items-center gap-2">
                        <Badge variant="outline">Draft</Badge>
                        <Badge variant="secondary">{notice.targetAudience}</Badge>
                        <Button
                          size="sm"
                          onClick={() => handlePublish(notice.noticeId)}
                          disabled={isPublishing === notice.noticeId}
                        >
                          {isPublishing === notice.noticeId ? (
                            <Spinner size="sm" />
                          ) : (
                            <>
                              <Send className="mr-1 h-3 w-3" />
                              Publish
                            </>
                          )}
                        </Button>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <p className="text-sm whitespace-pre-wrap">{notice.body}</p>
                    <div className="mt-3">
                      <AttachmentList entityId={notice.noticeId} entityType="notice" />
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">
                      Created: {formatDate(notice.createdAt)}
                    </p>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}

          {published.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-lg font-semibold">Published</h2>
              {published.map((notice) => (
                <Card key={notice.noticeId}>
                  <CardHeader className="pb-2">
                    <div className="flex items-start justify-between gap-2">
                      <CardTitle className="text-lg">{notice.title}</CardTitle>
                      <div className="flex items-center gap-2">
                        <Badge>Published</Badge>
                        <Badge variant="secondary">{notice.targetAudience}</Badge>
                      </div>
                    </div>
                    {notice.publishedAt && (
                      <p className="text-xs text-muted-foreground">
                        Published: {formatDate(notice.publishedAt)}
                      </p>
                    )}
                  </CardHeader>
                  <CardContent>
                    <p className="text-sm whitespace-pre-wrap">{notice.body}</p>
                    <div className="mt-3">
                      <AttachmentList entityId={notice.noticeId} entityType="notice" />
                    </div>
                    {notice.expiresAt && (
                      <p className="mt-2 text-xs text-muted-foreground">
                        Expires: {formatDate(notice.expiresAt)}
                      </p>
                    )}
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
