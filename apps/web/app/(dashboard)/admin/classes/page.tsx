"use client";

import * as React from "react";
import Link from "next/link";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ClassFilterBar } from "@/components/classes/filter-bar";
import { useClassFilters } from "@/hooks/use-class-filters";
import { applyClassFilter } from "@/lib/classes/filter-schema";
import { ArrowRight, Pencil, Plus, School } from "lucide-react";
import type {
  ClassItem,
  ClassMutationResponse,
  CreateClassRequest,
  UpdateClassRequest,
} from "@/lib/types/student";

export default function AdminClassesPage(): React.ReactElement {
  const { filters, setFilter, clearAll, activeCount } = useClassFilters();

  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  // Dialog state for create/edit — unchanged from the previous page.
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingClassId, setEditingClassId] = React.useState<string | null>(null);
  const [name, setName] = React.useState("");
  const [section, setSection] = React.useState("");
  const [academicYear, setAcademicYear] = React.useState("");
  const [formError, setFormError] = React.useState("");
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const searchInputRef = React.useRef<HTMLInputElement | null>(null);

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
    void fetchClasses();
  }, [fetchClasses]);

  // Slash + Cmd/Ctrl+K shortcuts for search focus.
  React.useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      const target = event.target as HTMLElement | null;
      const isEditable =
        target?.tagName === "INPUT" ||
        target?.tagName === "TEXTAREA" ||
        target?.isContentEditable;
      if (isEditable) return;
      if (event.key === "/" || (event.key.toLowerCase() === "k" && (event.metaKey || event.ctrlKey))) {
        event.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const availableYears = React.useMemo(() => {
    const years = new Set<string>();
    for (const item of classes) {
      if (item.academicYear) years.add(item.academicYear);
    }
    return Array.from(years).sort((a, b) => b.localeCompare(a));
  }, [classes]);

  const visibleClasses = React.useMemo(
    () => applyClassFilter(classes, filters),
    [classes, filters]
  );

  const totalStudents = React.useMemo(
    () => classes.reduce((total, item) => total + item.studentCount, 0),
    [classes]
  );

  const resetForm = (): void => {
    setEditingClassId(null);
    setName("");
    setSection("");
    setAcademicYear("");
    setFormError("");
  };

  const openCreateDialog = (): void => {
    setSuccessMessage("");
    resetForm();
    setDialogOpen(true);
  };

  const openEditDialog = (classItem: ClassItem): void => {
    setSuccessMessage("");
    setFormError("");
    setEditingClassId(classItem.id);
    setName(classItem.name);
    setSection(classItem.section);
    setAcademicYear(classItem.academicYear);
    setDialogOpen(true);
  };

  const closeDialog = (): void => {
    setDialogOpen(false);
    resetForm();
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

      closeDialog();
      void fetchClasses();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Failed to save class.");
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
          <Button size="sm" onClick={openCreateDialog}>
            <Plus className="h-4 w-4" />
            Add Class
          </Button>
        )}
        stats={[
          { label: "Classes", value: classes.length.toString() },
          { label: "Students", value: totalStudents.toString() },
        ]}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
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
          action={{ label: "Add Class", onClick: openCreateDialog }}
        />
      ) : (
        <PageSection className="space-y-5">
          <ClassFilterBar
            filters={filters}
            onFilterChange={setFilter}
            onClearAll={clearAll}
            activeCount={activeCount}
            availableYears={availableYears}
            searchInputRef={searchInputRef}
          />

          <p className="text-sm text-muted-foreground">
            Showing {visibleClasses.length} of {classes.length} class
            {classes.length !== 1 ? "es" : ""}
            {activeCount > 0 ? " (filtered)" : ""}.
          </p>

          {visibleClasses.length === 0 ? (
            <div className="rounded-[24px] border border-dashed border-border/70 bg-card/60 p-8 text-center shadow-[0_18px_46px_-34px_rgba(15,40,69,0.3)] dark:bg-card/80">
              <p className="text-sm font-medium text-foreground">
                No classes match your filters.
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                Try a different search term, change the year, or clear everything.
              </p>
              <Button size="sm" variant="outline" onClick={clearAll} className="mt-4">
                Clear filters
              </Button>
            </div>
          ) : (
            <ul className="grid gap-3 lg:grid-cols-2" role="list">
              {visibleClasses.map((classItem) => (
                <li key={classItem.id} className="relative">
                  <Link
                    href={`/admin/classes/${classItem.id}`}
                    className="focus-ring group block rounded-[24px] border border-border/70 bg-card/80 p-4 pr-14 shadow-[0_18px_46px_-34px_rgba(15,40,69,0.42)] transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:bg-card/95 hover:shadow-[0_22px_56px_-32px_rgba(15,40,69,0.48)] dark:bg-card/90"
                    aria-label={`View class ${classItem.name}${classItem.section ? ` ${classItem.section}` : ""}`}
                  >
                    <div className="flex items-start gap-4">
                      <div
                        aria-hidden="true"
                        className="flex h-14 w-14 shrink-0 items-center justify-center rounded-[18px] bg-primary/8 text-lg font-semibold text-primary ring-1 ring-inset ring-primary/15"
                      >
                        {classItem.name}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="text-lg font-semibold text-foreground">
                            Class {classItem.name}
                            {classItem.section ? ` ${classItem.section}` : ""}
                          </p>
                          {classItem.section && (
                            <span className="inline-flex items-center rounded-full border border-border/60 bg-muted/50 px-2 py-0.5 text-[11px] font-medium uppercase tracking-[0.14em] text-muted-foreground">
                              Section {classItem.section}
                            </span>
                          )}
                        </div>
                        <p className="mt-0.5 text-sm text-muted-foreground">
                          Academic year {classItem.academicYear || "—"}
                        </p>
                        <p className="mt-2 text-xs uppercase tracking-[0.18em] text-muted-foreground">
                          <span className="text-sm font-semibold text-foreground">
                            {classItem.studentCount}
                          </span>{" "}
                          student{classItem.studentCount !== 1 ? "s" : ""} enrolled
                        </p>
                      </div>
                      <span
                        aria-hidden="true"
                        className="mt-1 inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-border/60 bg-card/70 text-muted-foreground transition-all group-hover:border-primary/40 group-hover:bg-primary/10 group-hover:text-primary"
                      >
                        <ArrowRight className="h-4 w-4" />
                      </span>
                    </div>
                  </Link>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      openEditDialog(classItem);
                    }}
                    aria-label={`Edit class ${classItem.name}${classItem.section ? ` ${classItem.section}` : ""}`}
                    className="focus-ring absolute right-3 top-3 inline-flex h-9 w-9 items-center justify-center rounded-full border border-border/60 bg-card/92 text-muted-foreground shadow-sm transition-all hover:border-primary/40 hover:bg-primary/10 hover:text-primary"
                  >
                    <Pencil className="h-4 w-4" aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </PageSection>
      )}

      <Dialog
        open={dialogOpen}
        onOpenChange={(next) => {
          if (!next) closeDialog();
          else setDialogOpen(true);
        }}
        title={editingClassId ? "Edit class" : "New class"}
        description={
          editingClassId
            ? "Update the class name, section, or academic year."
            : "Add a new class so students and teachers can be assigned."
        }
        footer={
          <>
            <Button
              type="button"
              variant="outline"
              onClick={closeDialog}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button type="submit" form="class-form" disabled={isSubmitting}>
              {isSubmitting ? (
                <Spinner size="sm" />
              ) : editingClassId ? (
                "Save changes"
              ) : (
                "Create class"
              )}
            </Button>
          </>
        }
      >
        <form id="class-form" onSubmit={handleSubmit} className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <Input
              label="Class name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. 5"
              disabled={isSubmitting}
              data-autofocus
            />
            <Input
              label="Section"
              value={section}
              onChange={(e) => setSection(e.target.value)}
              placeholder="e.g. A"
              disabled={isSubmitting}
            />
            <Input
              label="Academic year"
              value={academicYear}
              onChange={(e) => setAcademicYear(e.target.value)}
              placeholder="e.g. 2026-27"
              disabled={isSubmitting}
            />
          </div>
          {formError && <StatusBanner variant="error">{formError}</StatusBanner>}
        </form>
      </Dialog>
    </PageShell>
  );
}
