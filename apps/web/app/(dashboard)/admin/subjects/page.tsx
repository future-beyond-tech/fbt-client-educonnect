"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { EmptyState } from "@/components/shared/empty-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft, Plus, BookOpen } from "lucide-react";
import type { SubjectItem, CreateSubjectRequest } from "@/lib/types/teacher";
import type { MutationResponse } from "@/lib/types/student";

export default function AdminSubjectsPage(): React.ReactElement {
  const router = useRouter();
  const [subjects, setSubjects] = React.useState<SubjectItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  // Dialog state for create
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [newSubjectName, setNewSubjectName] = React.useState("");
  const [isCreating, setIsCreating] = React.useState(false);
  const [createError, setCreateError] = React.useState("");

  const fetchSubjects = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<SubjectItem[]>(API_ENDPOINTS.subjects);
      setSubjects(data);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load subjects."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    fetchSubjects();
  }, [fetchSubjects]);

  const openCreateDialog = (): void => {
    setSuccessMessage("");
    setNewSubjectName("");
    setCreateError("");
    setDialogOpen(true);
  };

  const closeDialog = (): void => {
    setDialogOpen(false);
    setNewSubjectName("");
    setCreateError("");
  };

  const handleCreate = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setCreateError("");
    setSuccessMessage("");

    const trimmedName = newSubjectName.trim();
    if (!trimmedName) {
      setCreateError("Subject name is required.");
      return;
    }

    if (trimmedName.length > 80) {
      setCreateError("Subject name must be 80 characters or fewer.");
      return;
    }

    setIsCreating(true);
    try {
      const body: CreateSubjectRequest = { name: trimmedName };
      const result = await apiPost<MutationResponse>(
        API_ENDPOINTS.subjects,
        body
      );
      setSuccessMessage(result.message);
      closeDialog();
      fetchSubjects();
    } catch (err) {
      setCreateError(
        err instanceof ApiError ? err.message : "Failed to create subject."
      );
    } finally {
      setIsCreating(false);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Subjects"
        description="Manage the subject catalog used when assigning teachers to classes."
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push("/admin/teachers")}
            aria-label="Back to teachers"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Teachers
          </Button>
        )}
        actions={(
          <Button size="sm" onClick={openCreateDialog}>
            <Plus className="h-4 w-4" />
            Add Subject
          </Button>
        )}
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        stats={[{ label: "Subjects", value: subjects.length.toString() }]}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchSubjects} />
      ) : subjects.length === 0 ? (
        <EmptyState
          title="No subjects yet"
          description="Add a subject to get started with teacher assignments."
          icon={
            <BookOpen
              className="h-8 w-8 text-muted-foreground"
              aria-hidden="true"
            />
          }
          action={{ label: "Add Subject", onClick: openCreateDialog }}
        />
      ) : (
        <PageSection className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {subjects.length} subject{subjects.length !== 1 ? "s" : ""}
          </p>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {subjects.map((subject) => (
              <div
                key={subject.id}
                className="flex items-center rounded-[24px] border border-border/70 bg-card/80 p-4 shadow-[0_18px_46px_-34px_rgba(15,40,69,0.42)] dark:bg-card/90"
              >
                <BookOpen
                  className="mr-3 h-5 w-5 shrink-0 text-muted-foreground"
                  aria-hidden="true"
                />
                <span className="font-medium text-foreground">
                  {subject.name}
                </span>
              </div>
            ))}
          </div>
        </PageSection>
      )}

      <Dialog
        open={dialogOpen}
        onOpenChange={(next) => {
          if (!next) closeDialog();
          else setDialogOpen(true);
        }}
        title="New subject"
        description="Add a subject to the catalog so teachers can be assigned to it."
        footer={
          <>
            <Button
              type="button"
              variant="outline"
              onClick={closeDialog}
              disabled={isCreating}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              form="subject-form"
              disabled={isCreating}
            >
              {isCreating ? <Spinner size="sm" /> : "Create subject"}
            </Button>
          </>
        }
      >
        <form id="subject-form" onSubmit={handleCreate} className="space-y-4">
          <Input
            id="subjectName"
            label="Subject name"
            value={newSubjectName}
            onChange={(e) => setNewSubjectName(e.target.value)}
            placeholder="e.g. Mathematics"
            disabled={isCreating}
            maxLength={80}
            data-autofocus
          />
          {createError && <StatusBanner variant="error">{createError}</StatusBanner>}
        </form>
      </Dialog>
    </PageShell>
  );
}
