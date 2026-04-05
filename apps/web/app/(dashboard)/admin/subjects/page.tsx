"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { EmptyState } from "@/components/shared/empty-state";
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
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push("/admin/teachers")}
          aria-label="Back to teachers"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div className="flex-1">
          <h1 className="text-3xl font-bold tracking-tight">Subjects</h1>
          <p className="text-muted-foreground">
            Manage subjects available for teacher assignments.
          </p>
        </div>
        <Button
          size="sm"
          onClick={() => {
            setShowCreateForm(!showCreateForm);
            setCreateError("");
          }}
        >
          <Plus className="mr-1 h-4 w-4" />
          Add Subject
        </Button>
      </div>

      {successMessage && (
        <div className="rounded-md bg-green-50 p-3 text-sm text-green-800 dark:bg-green-950 dark:text-green-200">
          {successMessage}
        </div>
      )}

      {showCreateForm && (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">New Subject</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleCreate} className="space-y-3">
              <div className="space-y-2">
                <label
                  htmlFor="subjectName"
                  className="block text-sm font-medium"
                >
                  Subject Name
                </label>
                <Input
                  id="subjectName"
                  value={newSubjectName}
                  onChange={(e) => setNewSubjectName(e.target.value)}
                  placeholder="e.g. Mathematics"
                  disabled={isCreating}
                  maxLength={80}
                  autoFocus
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
        </Card>
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
        <>
          <p className="text-sm text-muted-foreground">
            {subjects.length} subject{subjects.length !== 1 ? "s" : ""}
          </p>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {subjects.map((subject) => (
              <div
                key={subject.id}
                className="flex items-center rounded-lg border bg-card p-4"
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
        </>
      )}
    </div>
  );
}
