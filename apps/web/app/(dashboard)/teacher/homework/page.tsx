"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import {
  approveHomeworkAction,
  createHomeworkAction,
  rejectHomeworkAction,
  submitHomeworkForApprovalAction,
  updateHomeworkAction,
} from "@/lib/actions/homework-actions";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
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
import type { TeacherClassItem } from "@/lib/types/teacher";

interface HomeworkItem {
  homeworkId: string;
  classId: string;
  /**
   * Human-readable class name + section, denormalised by the API from the
   * Classes table. Exposed so every homework card / approval row can show
   * "5 A · Science" style labels without a per-page classId→name lookup.
   * Defaults to empty string when the class row is missing (should not
   * happen under current constraints, but we guard against it anyway).
   */
  className: string;
  section: string;
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

export default function TeacherHomeworkPage(): React.ReactElement {
  const [homework, setHomework] = React.useState<HomeworkItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [actionError, setActionError] = React.useState("");
  const [actionSuccess, setActionSuccess] = React.useState("");
  const [actionHomeworkId, setActionHomeworkId] = React.useState<string | null>(null);

  // Filters (based on assignments)
  const [classFilter, setClassFilter] = React.useState("");
  const [subjectFilter, setSubjectFilter] = React.useState("");

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

  // Teacher assignment metadata (classes + subjects)
  const [assignments, setAssignments] = React.useState<TeacherClassItem[]>([]);
  const [assignmentError, setAssignmentError] = React.useState("");
  const [isLoadingAssignments, setIsLoadingAssignments] = React.useState(true);

  const fetchAssignments = React.useCallback(async () => {
    setIsLoadingAssignments(true);
    setAssignmentError("");
    try {
      const data = await apiGet<TeacherClassItem[]>(API_ENDPOINTS.teachersMyClasses);
      setAssignments(data);
    } catch (err) {
      setAssignmentError(err instanceof ApiError ? err.message : "Failed to load assignments.");
    } finally {
      setIsLoadingAssignments(false);
    }
  }, []);

  const fetchHomework = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (classFilter) params.set("classId", classFilter);
      if (subjectFilter) params.set("subject", subjectFilter);
      const qs = params.toString();
      const url = qs ? `${API_ENDPOINTS.homework}?${qs}` : API_ENDPOINTS.homework;
      const data = await apiGet<HomeworkItem[]>(url);
      setHomework(data);
    } catch {
      setError("Failed to load homework.");
    } finally {
      setIsLoading(false);
    }
  }, [classFilter, subjectFilter]);

  React.useEffect(() => {
    fetchAssignments();
    fetchHomework();
  }, [fetchAssignments, fetchHomework]);

  const classesForTeacher = React.useMemo(() => {
    const map = new Map<string, { classId: string; className: string; section: string }>();
    for (const a of assignments) {
      if (!map.has(a.classId)) {
        map.set(a.classId, { classId: a.classId, className: a.className, section: a.section });
      }
    }
    return Array.from(map.values()).sort((a, b) => {
      const nameCompare = a.className.localeCompare(b.className);
      if (nameCompare !== 0) return nameCompare;
      return a.section.localeCompare(b.section);
    });
  }, [assignments]);

  const subjectsForSelectedClass = React.useMemo(() => {
    if (!createClassId) return [];
    return assignments
      .filter((a) => a.classId === createClassId)
      .map((a) => a.subject)
      .sort((a, b) => a.localeCompare(b));
  }, [assignments, createClassId]);

  const subjectsForFilterClass = React.useMemo(() => {
    const list =
      classFilter
        ? assignments.filter((a) => a.classId === classFilter)
        : assignments;
    return Array.from(new Set(list.map((a) => a.subject))).sort((a, b) => a.localeCompare(b));
  }, [assignments, classFilter]);

  React.useEffect(() => {
    // If class filter changes, keep subject filter valid
    if (!subjectFilter) return;
    if (subjectsForFilterClass.includes(subjectFilter)) return;
    setSubjectFilter("");
  }, [subjectFilter, subjectsForFilterClass]);

  // When opening the create form, default to the first assignment
  React.useEffect(() => {
    if (!showCreateForm) return;
    if (createClassId && createSubject) return;
    if (classesForTeacher.length === 0) return;

    const firstClassId = classesForTeacher[0]?.classId;
    if (!firstClassId) return;

    setCreateClassId(firstClassId);
    const firstSubject = assignments.find((a) => a.classId === firstClassId)?.subject ?? "";
    setCreateSubject(firstSubject);
  }, [assignments, classesForTeacher, createClassId, createSubject, showCreateForm]);

  // If the class changes, reset subject to the first subject for that class
  React.useEffect(() => {
    if (!showCreateForm) return;
    if (!createClassId) return;
    if (subjectsForSelectedClass.length === 0) return;
    if (createSubject && subjectsForSelectedClass.includes(createSubject)) return;
    setCreateSubject(subjectsForSelectedClass[0] ?? "");
  }, [createClassId, createSubject, showCreateForm, subjectsForSelectedClass]);

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
      const result = await createHomeworkAction({
        classId: createClassId,
        subject: createSubject,
        title: createTitle,
        description: createDescription,
        dueDate: createDueDate,
      });
      if (!result.ok) {
        setCreateError(
          result.formError ??
            Object.values(result.fieldErrors ?? {})[0] ??
            "Failed to create homework.",
        );
        return;
      }
      setSuccessMessage("Homework created.");
      setShowCreateForm(false);
      setCreateClassId("");
      setCreateSubject("");
      setCreateTitle("");
      setCreateDescription("");
      setCreateDueDate("");
      // Show attachment uploader for the newly created homework
      setNewHomeworkId(result.data.homeworkId);
      setNewHomeworkAttachments([]);
      fetchHomework();
    } catch {
      setCreateError("Failed to create homework.");
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
      const result = await updateHomeworkAction({
        homeworkId: editingId,
        title: editTitle,
        description: editDescription,
        dueDate: editDueDate,
      });
      if (!result.ok) {
        setEditError(
          result.formError ??
            Object.values(result.fieldErrors ?? {})[0] ??
            "Failed to update homework.",
        );
        return;
      }
      setSuccessMessage(result.data.message);
      setEditingId(null);
      fetchHomework();
    } catch {
      setEditError("Failed to update homework.");
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

  // Centralised display helper so every card / approval row formats the
  // class label identically ("5 A" when a section exists, just "5" when it
  // doesn't). Takes a loose shape rather than HomeworkItem so the same helper
  // works for TeacherClassItem-style data too.
  const formatClassLabel = (c: { className: string; section: string }): string => {
    const name = (c.className ?? "").trim();
    const section = (c.section ?? "").trim();
    if (!name && !section) return "";
    return section ? `${name} ${section}` : name;
  };

  const submitForApproval = async (id: string): Promise<void> => {
    setActionError("");
    setActionSuccess("");
    setActionHomeworkId(id);
    try {
      const result = await submitHomeworkForApprovalAction(id);
      if (!result.ok) {
        setActionError(result.formError ?? "Failed to submit homework for approval.");
        return;
      }
      setActionSuccess(result.data.message);
      fetchHomework();
    } catch {
      setActionError("Failed to submit homework for approval.");
    } finally {
      setActionHomeworkId(null);
    }
  };

  const approveHomework = async (id: string): Promise<void> => {
    setActionError("");
    setActionSuccess("");
    setActionHomeworkId(id);
    try {
      const result = await approveHomeworkAction(id);
      if (!result.ok) {
        setActionError(result.formError ?? "Failed to approve homework.");
        return;
      }
      setActionSuccess(result.data.message);
      fetchHomework();
    } catch {
      setActionError("Failed to approve homework.");
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
      const result = await rejectHomeworkAction({ homeworkId: id, reason });
      if (!result.ok) {
        setActionError(
          result.formError ??
            Object.values(result.fieldErrors ?? {})[0] ??
            "Failed to reject homework.",
        );
        return;
      }
      setActionSuccess(result.data.message);
      fetchHomework();
    } catch {
      setActionError("Failed to reject homework.");
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
            variant="premium"
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
            {assignmentError ? (
              <StatusBanner variant="error">{assignmentError}</StatusBanner>
            ) : null}
            {/* Live summary of the selected class + subject. Keeps the
                teacher oriented while they fill in the title/description
                below — especially important for teachers who teach multiple
                classes where the form header alone wouldn't disambiguate. */}
            {createClassId && (() => {
              const picked = classesForTeacher.find((c) => c.classId === createClassId);
              if (!picked) return null;
              return (
                <div className="flex flex-wrap items-center gap-2 rounded-md border border-dashed bg-muted/40 px-3 py-2 text-sm">
                  <span className="text-muted-foreground">Creating homework for</span>
                  <Badge variant="default">{formatClassLabel(picked)}</Badge>
                  {createSubject ? (
                    <>
                      <span className="text-muted-foreground">in</span>
                      <Badge variant="secondary">{createSubject}</Badge>
                    </>
                  ) : null}
                </div>
              );
            })()}
            <div className="grid gap-3 md:grid-cols-2">
              <Select
                label="Class"
                value={createClassId}
                onChange={(e) => setCreateClassId(e.target.value)}
                disabled={isCreating || isLoadingAssignments}
              >
                <option value="" disabled>
                  {isLoadingAssignments ? "Loading classes..." : "Select a class"}
                </option>
                {classesForTeacher.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    {c.className}
                    {c.section ? ` ${c.section}` : ""}
                  </option>
                ))}
              </Select>
              <Select
                label="Subject"
                value={createSubject}
                onChange={(e) => setCreateSubject(e.target.value)}
                disabled={isCreating || isLoadingAssignments || !createClassId}
              >
                <option value="" disabled>
                  {!createClassId
                    ? "Select a class first"
                    : isLoadingAssignments
                      ? "Loading subjects..."
                      : "Select a subject"}
                </option>
                {subjectsForSelectedClass.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </Select>
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
          {assignments.length > 0 ? (
            <div className="grid gap-3 sm:grid-cols-2 lg:max-w-2xl">
              <Select
                label="Filter by class"
                value={classFilter}
                onChange={(e) => setClassFilter(e.target.value)}
                disabled={isLoadingAssignments}
              >
                <option value="">All classes</option>
                {classesForTeacher.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    {c.className}
                    {c.section ? ` ${c.section}` : ""}
                  </option>
                ))}
              </Select>
              <Select
                label="Filter by subject"
                value={subjectFilter}
                onChange={(e) => setSubjectFilter(e.target.value)}
                disabled={isLoadingAssignments}
              >
                <option value="">All subjects</option>
                {subjectsForFilterClass.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </Select>
            </div>
          ) : null}

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
                          {/* Class-name Badge is the fix for the bug: the
                              approving class teacher needs to see *which*
                              class they're publishing into before clicking
                              "Approve & publish". Rendered first (before the
                              subject) so it's the left-most context chip. */}
                          {formatClassLabel(h) && (
                            <Badge variant="default">{formatClassLabel(h)}</Badge>
                          )}
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
                    <div className="min-w-0">
                      <CardTitle className="text-lg">{item.title}</CardTitle>
                      {/* Show class + subject together on a dedicated line
                          beneath the title so teachers with multiple classes
                          can always tell at a glance which class a given
                          homework is for. This is the visible part of the
                          "class name missing" bug fix. */}
                      {formatClassLabel(item) && (
                        <p className="mt-1 text-sm text-muted-foreground">
                          {formatClassLabel(item)} · {item.subject}
                        </p>
                      )}
                    </div>
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
