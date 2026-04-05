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
import { BookOpen, Pencil, Plus } from "lucide-react";
import { AttachmentUploader, type UploadedFile } from "@/components/shared/attachment-uploader";
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

  const handleCreate = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setCreateError("");
    setSuccessMessage("");

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

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Homework</h1>
          <p className="text-muted-foreground">
            Manage homework assignments for your classes.
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
          New Homework
        </Button>
      </div>

      {successMessage && (
        <div className="rounded-md bg-green-50 p-3 text-sm text-green-800 dark:bg-green-950 dark:text-green-200">
          {successMessage}
        </div>
      )}

      {newHomeworkId && (
        <Card>
          <CardContent className="space-y-3 p-4">
            <h3 className="font-semibold">Attach Files to Homework</h3>
            <p className="text-sm text-muted-foreground">
              Optionally attach images or PDFs to the homework you just created.
            </p>
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
          </CardContent>
        </Card>
      )}

      {showCreateForm && (
        <Card>
          <CardContent className="p-4">
            <form onSubmit={handleCreate} className="space-y-3">
              <h3 className="font-semibold">Create Homework</h3>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <label htmlFor="createClassId" className="text-sm font-medium">
                    Class ID
                  </label>
                  <Input
                    id="createClassId"
                    placeholder="Enter class ID"
                    value={createClassId}
                    onChange={(e) => setCreateClassId(e.target.value)}
                    disabled={isCreating}
                  />
                </div>
                <div className="space-y-1">
                  <label htmlFor="createSubject" className="text-sm font-medium">
                    Subject
                  </label>
                  <Input
                    id="createSubject"
                    placeholder="e.g. Mathematics"
                    value={createSubject}
                    onChange={(e) => setCreateSubject(e.target.value)}
                    disabled={isCreating}
                  />
                </div>
              </div>
              <div className="space-y-1">
                <label htmlFor="createTitle" className="text-sm font-medium">
                  Title
                </label>
                <Input
                  id="createTitle"
                  placeholder="Homework title"
                  value={createTitle}
                  onChange={(e) => setCreateTitle(e.target.value)}
                  disabled={isCreating}
                />
              </div>
              <div className="space-y-1">
                <label htmlFor="createDescription" className="text-sm font-medium">
                  Description
                </label>
                <textarea
                  id="createDescription"
                  placeholder="Homework description and instructions"
                  value={createDescription}
                  onChange={(e) => setCreateDescription(e.target.value)}
                  disabled={isCreating}
                  rows={3}
                  className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                />
              </div>
              <div className="space-y-1">
                <label htmlFor="createDueDate" className="text-sm font-medium">
                  Due Date
                </label>
                <Input
                  id="createDueDate"
                  type="date"
                  value={createDueDate}
                  onChange={(e) => setCreateDueDate(e.target.value)}
                  disabled={isCreating}
                />
              </div>
              {createError && (
                <p className="text-sm text-destructive">{createError}</p>
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
          </CardContent>
        </Card>
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
        <div className="space-y-3">
          {homework.map((item) =>
            editingId === item.homeworkId ? (
              <Card key={item.homeworkId}>
                <CardContent className="p-4">
                  <form onSubmit={handleUpdate} className="space-y-3">
                    <h3 className="font-semibold">Edit Homework</h3>
                    <div className="space-y-1">
                      <label htmlFor="editTitle" className="text-sm font-medium">
                        Title
                      </label>
                      <Input
                        id="editTitle"
                        value={editTitle}
                        onChange={(e) => setEditTitle(e.target.value)}
                        disabled={isUpdating}
                      />
                    </div>
                    <div className="space-y-1">
                      <label htmlFor="editDescription" className="text-sm font-medium">
                        Description
                      </label>
                      <textarea
                        id="editDescription"
                        value={editDescription}
                        onChange={(e) => setEditDescription(e.target.value)}
                        disabled={isUpdating}
                        rows={3}
                        className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                      />
                    </div>
                    <div className="space-y-1">
                      <label htmlFor="editDueDate" className="text-sm font-medium">
                        Due Date
                      </label>
                      <Input
                        id="editDueDate"
                        type="date"
                        value={editDueDate}
                        onChange={(e) => setEditDueDate(e.target.value)}
                        disabled={isUpdating}
                      />
                    </div>
                    {editError && (
                      <p className="text-sm text-destructive">{editError}</p>
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
                  <p className="text-sm text-muted-foreground mb-3">
                    {item.description}
                  </p>
                  <div className="flex items-center gap-2 text-sm mb-3">
                    <span className="text-muted-foreground">Due:</span>
                    <span className="font-medium">{formatDate(item.dueDate)}</span>
                  </div>
                  <AttachmentList entityId={item.homeworkId} entityType="homework" />
                </CardContent>
              </Card>
            )
          )}
        </div>
      )}
    </div>
  );
}
