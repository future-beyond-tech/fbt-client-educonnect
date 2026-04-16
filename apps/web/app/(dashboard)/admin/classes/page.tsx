"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { Pencil, Plus, School } from "lucide-react";
import type {
  ClassItem,
  ClassMutationResponse,
  CreateClassRequest,
  UpdateClassRequest,
} from "@/lib/types/student";

export default function AdminClassesPage(): React.ReactElement {
  const router = useRouter();
  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");
  const [showForm, setShowForm] = React.useState(false);
  const [editingClassId, setEditingClassId] = React.useState<string | null>(null);
  const [name, setName] = React.useState("");
  const [section, setSection] = React.useState("");
  const [academicYear, setAcademicYear] = React.useState("");
  const [formError, setFormError] = React.useState("");
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const fetchClasses = React.useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<ClassItem[]>(API_ENDPOINTS.classes);
      setClasses(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load classes.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    fetchClasses();
  }, [fetchClasses]);

  const resetForm = (): void => {
    setShowForm(false);
    setEditingClassId(null);
    setName("");
    setSection("");
    setAcademicYear("");
    setFormError("");
  };

  const openCreateForm = (): void => {
    setSuccessMessage("");
    setFormError("");
    setShowForm(true);
    setEditingClassId(null);
    setName("");
    setSection("");
    setAcademicYear("");
  };

  const openEditForm = (classItem: ClassItem): void => {
    setSuccessMessage("");
    setFormError("");
    setShowForm(true);
    setEditingClassId(classItem.id);
    setName(classItem.name);
    setSection(classItem.section);
    setAcademicYear(classItem.academicYear);
  };

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setFormError("");
    setSuccessMessage("");

    const trimmedName = name.trim();
    const trimmedSection = section.trim();
    const trimmedAcademicYear = academicYear.trim();

    if (!trimmedName || !trimmedSection || !trimmedAcademicYear) {
      setFormError("Class name, section, and academic year are required.");
      return;
    }

    setIsSubmitting(true);
    try {
      if (editingClassId) {
        const body: UpdateClassRequest = {
          name: trimmedName,
          section: trimmedSection,
          academicYear: trimmedAcademicYear,
        };
        const result = await apiPut<ClassMutationResponse>(
          `${API_ENDPOINTS.classes}/${editingClassId}`,
          body
        );
        setSuccessMessage(result.message);
      } else {
        const body: CreateClassRequest = {
          name: trimmedName,
          section: trimmedSection,
          academicYear: trimmedAcademicYear,
        };
        const result = await apiPost<ClassMutationResponse>(
          API_ENDPOINTS.classes,
          body
        );
        setSuccessMessage(result.message);
      }

      resetForm();
      fetchClasses();
    } catch (err) {
      setFormError(
        err instanceof ApiError ? err.message : "Failed to save class."
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Classes"
        description="Create and update class records so students and teachers can be assigned without depending on seeded data."
        icon={<School className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <Button size="sm" onClick={openCreateForm}>
            <Plus className="h-4 w-4" />
            Add Class
          </Button>
        )}
        stats={[
          { label: "Classes", value: classes.length.toString() },
          {
            label: "Students",
            value: classes.reduce((total, item) => total + item.studentCount, 0).toString(),
          },
        ]}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}

      {showForm && (
        <PageSection>
          <CardHeader className="px-0 pt-0">
            <CardTitle className="text-lg">
              {editingClassId ? "Edit Class" : "New Class"}
            </CardTitle>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            <form onSubmit={handleSubmit} className="max-w-3xl space-y-4">
              <div className="grid gap-4 md:grid-cols-3">
                <Input
                  label="Class Name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="e.g. 5"
                  disabled={isSubmitting}
                />
                <Input
                  label="Section"
                  value={section}
                  onChange={(e) => setSection(e.target.value)}
                  placeholder="e.g. A"
                  disabled={isSubmitting}
                />
                <Input
                  label="Academic Year"
                  value={academicYear}
                  onChange={(e) => setAcademicYear(e.target.value)}
                  placeholder="e.g. 2026-27"
                  disabled={isSubmitting}
                />
              </div>
              {formError && <StatusBanner variant="error">{formError}</StatusBanner>}
              <div className="flex gap-2">
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? <Spinner size="sm" /> : editingClassId ? "Save Changes" : "Create Class"}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={resetForm}
                  disabled={isSubmitting}
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
        <ErrorState title="Error" message={error} onRetry={fetchClasses} />
      ) : classes.length === 0 ? (
        <EmptyState
          title="No classes yet"
          description="Create your first class to unlock student enrollment and teacher assignment."
          icon={<School className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
          action={{ label: "Add Class", onClick: openCreateForm }}
        />
      ) : (
        <PageSection className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {classes.length} class{classes.length !== 1 ? "es" : ""} available for enrollment and teacher assignment.
          </p>
          <div className="grid gap-3 lg:grid-cols-2">
            {classes.map((classItem) => (
              <div
                key={classItem.id}
                className="rounded-[24px] border border-border/70 bg-card/80 p-4 shadow-[0_18px_46px_-34px_rgba(15,40,69,0.42)] dark:bg-card/90"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-lg font-semibold text-foreground">
                      {classItem.name}
                      {classItem.section ? ` ${classItem.section}` : ""}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Academic year {classItem.academicYear || "—"}
                    </p>
                    <p className="mt-2 text-xs uppercase tracking-[0.18em] text-muted-foreground">
                      {classItem.studentCount} student{classItem.studentCount !== 1 ? "s" : ""}
                    </p>
                  </div>
                  <div className="flex flex-col gap-2 sm:flex-row">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => router.push(`/admin/classes/${classItem.id}`)}
                    >
                      View
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => openEditForm(classItem)}
                    >
                      <Pencil className="h-4 w-4" />
                      Edit
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </PageSection>
      )}
    </PageShell>
  );
}
