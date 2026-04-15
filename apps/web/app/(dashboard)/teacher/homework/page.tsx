"use client";

import * as React from "react";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { BookOpen, Pencil, Plus } from "lucide-react";
import { AttachmentUploader, type UploadedFile } from "@/components/shared/attachment-uploader";
import { AttachmentList } from "@/components/shared/attachment-list";
import type { AttachmentItem } from "@/lib/types/attachment";

interface HomeworkItem {
  homeworkId: string;
  classId: string;
  subject: string;
  title: string;
  description: string;
  dueDate: string;
  isEditable: boolean;
  status: "Draft" | "PendingApproval" | "Published" | "Rejected";
  submittedAt?: string | null;
  approvedAt?: string | null;
  approvedById?: string | null;
  rejectedAt?: string | null;
  rejectedById?: string | null;
  rejectedReason?: string | null;
  canSubmitForApproval: boolean;
  canApproveOrReject: boolean;
  publishedAt: string;
}

interface CreateHomeworkResponse {
  homeworkId: string;
  message: string;
}

interface UpdateHomeworkResponse {
  message: string;
}

export default function TeacherHomeworkPage(): React.ReactElement {
  const [homework, setHomework] = React.useState<HomeworkItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [actionError, setActionError] = React.useState("");
  const [actionSuccess, setActionSuccess] = React.useState("");
  const [actionHomeworkId, setActionHomeworkId] = React.useState<string | null>(null);

  // Create form state
  const [showCreateForm, setShowCreateForm] = React.useState(false);
  const [createClassId, setCreateClassId] = React.useState("");
  const [createSubject, setCreateSubject] = React.useState("");
  const [createTitle, setCreateTitle] = React.useState("");
  const [createDescription, setCreateDescription] = React.useState("");
  const [createDueDate, setCreateDueDate] = React.useState("");
  const [createError, setCreateError] = React.useState("");
  const [isCreating, setIsCreating] = React.useState(false);

  // Edit form state
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [editTitle, setEditTitle] = React.useState("");
  const [editDescription, setEditDescription] = React.useState("");
  const [editDueDate, setEditDueDate] = React.useState("");
  const [editError, setEditError] = React.useState("");
  const [isUpdating, setIsUpdating] = React.useState(false);

  const [successMessage, setSuccessMessage] = React.useState("");

  // Post-create attachment flow
  const [newHomeworkId, setNewHomeworkId] = React.useState<string | null>(null);
  const [newHomeworkAttachments, setNewHomeworkAttachments] = React.useState<UploadedFile[]>([]);
  const [managingAttachmentsForId, setManagingAttachmentsForId] = React.useState<string | null>(null);
  const [loadingAttachmentsForId, setLoadingAttachmentsForId] = React.useState<string | null>(null);
  const [attachmentError, setAttachmentError] = React.useState("");
  const [attachmentsByHomeworkId, setAttachmentsByHomeworkId] = React.useState<
    Record<string, UploadedFile[]>
  >({});

  const fetchHomework = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<HomeworkItem[]>(API_ENDPOINTS.homework);
      setHomework(data);
    } catch {
      setError("Failed to load homework.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    fetchHomework();
  }, [fetchHomework]);

  const mapAttachments = React.useCallback(
    (attachments: AttachmentItem[]): UploadedFile[] =>
      attachments.map((attachment) => ({
        attachmentId: attachment.id,
        fileName: attachment.fileName,
        contentType: attachment.contentType,
        sizeBytes: attachment.sizeBytes,
      })),
    []
  );

  const loadHomeworkAttachments = React.useCallback(
    async (homeworkId: string): Promise<void> => {
      setLoadingAttachmentsForId(homeworkId);
      setAttachmentError("");

      try {
        const attachments = await apiGet<AttachmentItem[]>(
          `${API_ENDPOINTS.attachments}?entityId=${homeworkId}&entityType=homework`
        );

        setAttachmentsByHomeworkId((prev) => ({
          ...prev,
          [homeworkId]: mapAttachments(attachments),
        }));
      } catch (err) {
        setAttachmentError(
          err instanceof ApiError ? err.message : "Failed to load attachments."
        );
      } finally {
        setLoadingAttachmentsForId((current) =>
          current === homeworkId ? null : current
        );
      }
    },
    [mapAttachments]
  );

  const toggleAttachmentManager = React.useCallback(
    async (homeworkId: string): Promise<void> => {
      if (managingAttachmentsForId === homeworkId) {
        setManagingAttachmentsForId(null);
        setAttachmentError("");
        return;
      }

      setManagingAttachmentsForId(homeworkId);

      if (!attachmentsByHomeworkId[homeworkId]) {
        await loadHomeworkAttachments(homeworkId);
      }
    },
    [attachmentsByHomeworkId, loadHomeworkAttachments, managingAttachmentsForId]
  );

  const handleCreate = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setCreateError("");
    setSuccessMessage("");
    setActionError("");
    setActionSuccess("");

    if (!createClassId || !createSubject || !createTitle || !createDescription || !createDueDate) {
      setCreateError("All fields are required.");
      return;
    }

    setIsCreating(true);
    try {
      const response = await apiPost<CreateHomeworkResponse>(API_ENDPOINTS.homework, {
        classId: createClassId,
        subject: createSubject,
        title: createTitle,
        description: createDescription,
        dueDate: createDueDate,
      });
      setSuccessMessage(response.message);
      setShowCreateForm(false);
      setCreateClassId("");
      setCreateSubject("");
      setCreateTitle("");
      setCreateDescription("");
      setCreateDueDate("");
      // Show attachment uploader for the newly created homework
      setNewHomeworkId(response.homeworkId);
      setNewHomeworkAttachments([]);
      fetchHomework();
    } catch (err) {
      setCreateError(err instanceof ApiError ? err.message : "Failed to create homework.");
    } finally {
      setIsCreating(false);
    }
  };

  const startEdit = (item: HomeworkItem): void => {
    setEditingId(item.homeworkId);
    setEditTitle(item.title);
    setEditDescription(item.description);
    setEditDueDate(item.dueDate);
    setEditError("");
  };

  const handleUpdate = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    if (!editingId) return;
    setEditError("");
    setSuccessMessage("");
    setActionError("");
    setActionSuccess("");

    setIsUpdating(true);
    try {
      const response = await apiPut<UpdateHomeworkResponse>(
        `${API_ENDPOINTS.homework}/${editingId}`,
        {
          homeworkId: editingId,
          title: editTitle,
          description: editDescription,
          dueDate: editDueDate,
        }
      );
      setSuccessMessage(response.message);
      setEditingId(null);
      fetchHomework();
    } catch (err) {
      setEditError(err instanceof ApiError ? err.message : "Failed to update homework.");
    } finally {
      setIsUpdating(false);
    }
  };

  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
    });
  };

  const getStatusBadgeVariant = (
    status: HomeworkItem["status"]
  ): "secondary" | "destructive" | "outline" | undefined => {
    if (status === "Draft") return "secondary";
    if (status === "PendingApproval") return "outline";
    if (status === "Rejected") return "destructive";
    return undefined;
  };

  const getStatusLabel = (status: HomeworkItem["status"]): string => {
    if (status === "PendingApproval") return "Pending approval";
    return status;
  };

  const submitForApproval = async (id: string): Promise<void> => {
    setActionError("");
    setActionSuccess("");
    setActionHomeworkId(id);
    try {
      const response = await apiPut<{ message: string }>(`${API_ENDPOINTS.homework}/${id}/submit`, {});
      setActionSuccess(response.message);
      fetchHomework();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to submit homework for approval.");
    } finally {
      setActionHomeworkId(null);
    }
  };

  const approveHomework = async (id: string): Promise<void> => {
    setActionError("");
    setActionSuccess("");
    setActionHomeworkId(id);
    try {
      const response = await apiPut<{ message: string }>(`${API_ENDPOINTS.homework}/${id}/approve`, {});
      setActionSuccess(response.message);
      fetchHomework();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to approve homework.");
    } finally {
      setActionHomeworkId(null);
    }
  };

  const rejectHomework = async (id: string): Promise<void> => {
    const reason = window.prompt("Rejection reason");
    if (!reason) return;
    setActionError("");
    setActionSuccess("");
    setActionHomeworkId(id);
    try {
      const response = await apiPut<{ message: string }>(`${API_ENDPOINTS.homework}/${id}/reject`, {
        reason,
      });
      setActionSuccess(response.message);
      fetchHomework();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "Failed to reject homework.");
    } finally {
      setActionHomeworkId(null);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title="Homework"
        description="Create assignments, update active work, and attach supporting files for each class."
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <Button
            onClick={() => {
              setShowCreateForm(!showCreateForm);
              setCreateError("");
              setSuccessMessage("");
            }}
            size="sm"
          >
            <Plus className="h-4 w-4" />
            New Homework
          </Button>
        )}
        stats={[
          { label: "Assignments", value: homework.length.toString() },
          { label: "Composer", value: showCreateForm ? "Open" : "Closed" },
        ]}
      />

      {actionError && (
        <StatusBanner variant="error">{actionError}</StatusBanner>
      )}
      {actionSuccess && (
        <StatusBanner variant="success">{actionSuccess}</StatusBanner>
      )}

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}

      {newHomeworkId && (
        <PageSection className="space-y-4">
          <div>
            <h3 className="text-lg font-semibold">Attach Files to Homework</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              Optionally attach PDF or Word documents to the homework you just created.
            </p>
          </div>
          <AttachmentUploader
            entityId={newHomeworkId}
            entityType="homework"
            existingAttachments={newHomeworkAttachments}
            onAttachmentsChange={setNewHomeworkAttachments}
          />
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => {
              setNewHomeworkId(null);
              setNewHomeworkAttachments([]);
              fetchHomework();
            }}
          >
            Done
          </Button>
        </PageSection>
      )}

      {showCreateForm && (
        <PageSection>
          <form onSubmit={handleCreate} className="space-y-4">
            <h3 className="text-lg font-semibold">Create Homework</h3>
            <div className="grid gap-3 md:grid-cols-2">
              <Input
                id="createClassId"
                label="Class ID"
                placeholder="Enter class ID"
                value={createClassId}
                onChange={(e) => setCreateClassId(e.target.value)}
                disabled={isCreating}
              />
              <Input
                id="createSubject"
                label="Subject"
                placeholder="e.g. Mathematics"
                value={createSubject}
                onChange={(e) => setCreateSubject(e.target.value)}
                disabled={isCreating}
              />
            </div>
            <Input
              id="createTitle"
              label="Title"
              placeholder="Homework title"
              value={createTitle}
              onChange={(e) => setCreateTitle(e.target.value)}
              disabled={isCreating}
            />
            <Textarea
              id="createDescription"
              label="Description"
              placeholder="Homework description and instructions"
              value={createDescription}
              onChange={(e) => setCreateDescription(e.target.value)}
              disabled={isCreating}
              rows={4}
            />
            <Input
              id="createDueDate"
              label="Due Date"
              type="date"
              value={createDueDate}
              onChange={(e) => setCreateDueDate(e.target.value)}
              disabled={isCreating}
            />
            {createError && (
              <StatusBanner variant="error">{createError}</StatusBanner>
            )}
            <div className="flex gap-2">
              <Button type="submit" size="sm" disabled={isCreating}>
                {isCreating ? <Spinner size="sm" /> : "Create"}
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
          description="You haven't created any homework yet."
          icon={<BookOpen className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
          action={{
            label: "Create Homework",
            onClick: () => setShowCreateForm(true),
          }}
        />
      ) : (
        <PageSection className="space-y-4">
          {homework.some((h) => h.status === "PendingApproval" && h.canApproveOrReject) && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-lg">Approvals</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {homework
                  .filter((h) => h.status === "PendingApproval" && h.canApproveOrReject)
                  .map((h) => (
                    <div
                      key={h.homeworkId}
                      className="flex flex-col gap-2 rounded-lg border p-3 sm:flex-row sm:items-center sm:justify-between"
                    >
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="truncate font-medium">{h.title}</span>
                          <Badge variant="secondary">{h.subject}</Badge>
                          <Badge variant={getStatusBadgeVariant(h.status)}>{getStatusLabel(h.status)}</Badge>
                        </div>
                        <div className="mt-1 text-sm text-muted-foreground">Due {formatDate(h.dueDate)}</div>
                      </div>
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          onClick={() => approveHomework(h.homeworkId)}
                          disabled={actionHomeworkId === h.homeworkId}
                        >
                          {actionHomeworkId === h.homeworkId ? <Spinner size="sm" /> : "Approve & publish"}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => rejectHomework(h.homeworkId)}
                          disabled={actionHomeworkId === h.homeworkId}
                        >
                          Reject
                        </Button>
                      </div>
                    </div>
                  ))}
              </CardContent>
            </Card>
          )}

          {homework.map((item) =>
            editingId === item.homeworkId ? (
              <Card key={item.homeworkId}>
                <CardContent className="p-4">
                  <form onSubmit={handleUpdate} className="space-y-4">
                    <h3 className="text-lg font-semibold">Edit Homework</h3>
                    <Input
                      id="editTitle"
                      label="Title"
                      value={editTitle}
                      onChange={(e) => setEditTitle(e.target.value)}
                      disabled={isUpdating}
                    />
                    <Textarea
                      id="editDescription"
                      label="Description"
                      value={editDescription}
                      onChange={(e) => setEditDescription(e.target.value)}
                      disabled={isUpdating}
                      rows={4}
                    />
                    <Input
                      id="editDueDate"
                      label="Due Date"
                      type="date"
                      value={editDueDate}
                      onChange={(e) => setEditDueDate(e.target.value)}
                      disabled={isUpdating}
                    />
                    {editError && (
                      <StatusBanner variant="error">{editError}</StatusBanner>
                    )}
                    <div className="flex gap-2">
                      <Button type="submit" size="sm" disabled={isUpdating}>
                        {isUpdating ? <Spinner size="sm" /> : "Save"}
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setEditingId(null)}
                        disabled={isUpdating}
                      >
                        Cancel
                      </Button>
                    </div>
                  </form>
                </CardContent>
              </Card>
            ) : (
              <Card key={item.homeworkId}>
                <CardHeader className="pb-2">
                  <div className="flex items-start justify-between gap-2">
                    <CardTitle className="text-lg">{item.title}</CardTitle>
                    <div className="flex items-center gap-2">
                      <Badge variant="secondary">{item.subject}</Badge>
                      <Badge variant={getStatusBadgeVariant(item.status)}>{getStatusLabel(item.status)}</Badge>
                      {item.isEditable && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => startEdit(item)}
                          aria-label="Edit homework"
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <p className="mb-3 text-sm text-muted-foreground">
                    {item.description}
                  </p>
                  {item.status === "Rejected" && item.rejectedReason && (
                    <div className="mb-3 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm">
                      <div className="font-medium text-destructive">Rejected</div>
                      <div className="mt-1 text-muted-foreground">{item.rejectedReason}</div>
                    </div>
                  )}
                  <div className="mb-3 flex items-center gap-2 text-sm">
                    <span className="text-muted-foreground">Due:</span>
                    <span className="font-medium">{formatDate(item.dueDate)}</span>
                  </div>
                  <div className="space-y-3">
                    <div className="flex flex-wrap gap-2">
                      {item.canSubmitForApproval && (
                        <>
                          <Button
                            size="sm"
                            onClick={() => submitForApproval(item.homeworkId)}
                            disabled={actionHomeworkId === item.homeworkId}
                          >
                            {actionHomeworkId === item.homeworkId ? (
                              <Spinner size="sm" />
                            ) : (
                              "Submit for approval"
                            )}
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => startEdit(item)}
                            disabled={actionHomeworkId === item.homeworkId}
                          >
                            Edit draft
                          </Button>
                        </>
                      )}

                      {item.isEditable && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            void toggleAttachmentManager(item.homeworkId);
                          }}
                          disabled={loadingAttachmentsForId === item.homeworkId}
                        >
                          {loadingAttachmentsForId === item.homeworkId ? (
                            <Spinner size="sm" />
                          ) : managingAttachmentsForId === item.homeworkId ? (
                            "Hide attachments"
                          ) : (
                            "Manage attachments"
                          )}
                        </Button>
                      )}
                    </div>

                    {item.isEditable && managingAttachmentsForId === item.homeworkId ? (
                      <div className="space-y-3">
                        {attachmentError ? (
                          <StatusBanner variant="error">{attachmentError}</StatusBanner>
                        ) : null}
                        <AttachmentUploader
                          entityId={item.homeworkId}
                          entityType="homework"
                          existingAttachments={attachmentsByHomeworkId[item.homeworkId] ?? []}
                          onAttachmentsChange={(attachments) => {
                            setAttachmentsByHomeworkId((prev) => ({
                              ...prev,
                              [item.homeworkId]: attachments,
                            }));
                          }}
                        />
                      </div>
                    ) : (
                      <AttachmentList entityId={item.homeworkId} entityType="homework" />
                    )}
                  </div>
                </CardContent>
              </Card>
            )
          )}
        </PageSection>
      )}
    </PageShell>
  );
}
