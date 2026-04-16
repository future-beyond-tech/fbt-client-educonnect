"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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

  // Create form
  const [showCreateForm, setShowCreateForm] = React.useState(false);
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
      setShowCreateForm(false);
      setNewSubjectName("");
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
          <Button
            size="sm"
            onClick={() => {
              setShowCreateForm(!showCreateForm);
              setCreateError("");
            }}
          >
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

      {showCreateForm && (
        <PageSection>
          <CardHeader className="px-0 pt-0">
            <CardTitle className="text-lg">New Subject</CardTitle>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            <form onSubmit={handleCreate} className="space-y-3">
              <Input
                id="subjectName"
                label="Subject Name"
                value={newSubjectName}
                onChange={(e) => setNewSubjectName(e.target.value)}
                placeholder="e.g. Mathematics"
                disabled={isCreating}
                maxLength={80}
                autoFocus
              />
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
                  onClick={() => {
                    setShowCreateForm(false);
                    setNewSubjectName("");
                    setCreateError("");
                  }}
                  disabled={isCreating}
                >
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </PageSection>
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
    </PageShell>
  );
}
