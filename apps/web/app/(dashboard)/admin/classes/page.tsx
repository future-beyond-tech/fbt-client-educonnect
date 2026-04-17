"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Dialog } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowRight, Pencil, Plus, School, Search, X } from "lucide-react";
import { cn } from "@/lib/utils";
import type {
  ClassItem,
  ClassMutationResponse,
  CreateClassRequest,
  UpdateClassRequest,
} from "@/lib/types/student";

type SortKey = "name" | "students-desc" | "students-asc" | "year-desc";

const SORT_LABELS: Record<SortKey, string> = {
  name: "Name (A–Z)",
  "students-desc": "Most students",
  "students-asc": "Fewest students",
  "year-desc": "Newest academic year",
};

function parseSortKey(raw: string | null): SortKey {
  if (raw === "students-desc" || raw === "students-asc" || raw === "year-desc") {
    return raw;
  }
  return "name";
}

export default function AdminClassesPage(): React.ReactElement {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  // Filter / sort / search state — initialised from URL.
  const [searchTerm, setSearchTerm] = React.useState<string>(
    searchParams.get("q") ?? ""
  );
  const [sortKey, setSortKey] = React.useState<SortKey>(
    parseSortKey(searchParams.get("sort"))
  );
  const [yearFilter, setYearFilter] = React.useState<string>(
    searchParams.get("year") ?? ""
  );

  // Dialog state for create/edit
  const [dialogOpen, setDialogOpen] = React.useState(false);
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

  // Keep the URL in sync with filter/sort/search (replaceState — no new history entry).
  React.useEffect(() => {
    const params = new URLSearchParams();
    if (searchTerm.trim()) params.set("q", searchTerm.trim());
    if (sortKey !== "name") params.set("sort", sortKey);
    if (yearFilter) params.set("year", yearFilter);
    const query = params.toString();
    const url = query ? `${pathname}?${query}` : pathname;
    router.replace(url, { scroll: false });
  }, [pathname, router, searchTerm, sortKey, yearFilter]);

  const academicYears = React.useMemo(() => {
    const years = new Set<string>();
    classes.forEach((item) => {
      if (item.academicYear) years.add(item.academicYear);
    });
    return Array.from(years).sort((a, b) => b.localeCompare(a));
  }, [classes]);

  const visibleClasses = React.useMemo(() => {
    const needle = searchTerm.trim().toLowerCase();
    let filtered = classes;

    if (yearFilter) {
      filtered = filtered.filter((item) => item.academicYear === yearFilter);
    }

    if (needle) {
      filtered = filtered.filter((item) => {
        const haystack = `${item.name} ${item.section} ${item.academicYear}`.toLowerCase();
        return haystack.includes(needle);
      });
    }

    const sorted = [...filtered];
    switch (sortKey) {
      case "students-desc":
        sorted.sort((a, b) => b.studentCount - a.studentCount || a.name.localeCompare(b.name));
        break;
      case "students-asc":
        sorted.sort((a, b) => a.studentCount - b.studentCount || a.name.localeCompare(b.name));
        break;
      case "year-desc":
        sorted.sort((a, b) =>
          b.academicYear.localeCompare(a.academicYear) ||
          a.name.localeCompare(b.name) ||
          a.section.localeCompare(b.section)
        );
        break;
      case "name":
      default:
        sorted.sort((a, b) =>
          a.name.localeCompare(b.name) ||
          a.section.localeCompare(b.section) ||
          b.academicYear.localeCompare(a.academicYear)
        );
        break;
    }
    return sorted;
  }, [classes, searchTerm, sortKey, yearFilter]);

  const totalStudents = React.useMemo(
    () => classes.reduce((total, item) => total + item.studentCount, 0),
    [classes]
  );

  const isFiltering = !!searchTerm.trim() || !!yearFilter || sortKey !== "name";

  const resetFilters = (): void => {
    setSearchTerm("");
    setYearFilter("");
    setSortKey("name");
  };

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
          {/* Toolbar: search + sort + year filter chips */}
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div className="flex flex-1 flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex-1">
                <label className="mb-1.5 block text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground" htmlFor="class-search">
                  Search
                </label>
                <div className="relative">
                  <Search
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
                  />
                  <input
                    id="class-search"
                    type="search"
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    placeholder="Search by class, section, or year"
                    className="focus-ring h-11 w-full rounded-[18px] border border-border/70 bg-card/80 pl-10 pr-10 text-sm text-foreground placeholder:text-muted-foreground shadow-[0_12px_36px_-30px_rgba(15,40,69,0.4)] dark:bg-card/90"
                  />
                  {searchTerm && (
                    <button
                      type="button"
                      onClick={() => setSearchTerm("")}
                      aria-label="Clear search"
                      className="focus-ring absolute right-2 top-1/2 inline-flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-full text-muted-foreground hover:bg-muted/60 hover:text-foreground"
                    >
                      <X className="h-4 w-4" aria-hidden="true" />
                    </button>
                  )}
                </div>
              </div>
              <div className="w-full sm:w-56">
                <Select
                  label="Sort by"
                  value={sortKey}
                  onChange={(e) => setSortKey(parseSortKey(e.target.value))}
                >
                  {(Object.keys(SORT_LABELS) as SortKey[]).map((key) => (
                    <option key={key} value={key}>
                      {SORT_LABELS[key]}
                    </option>
                  ))}
                </Select>
              </div>
            </div>
            {isFiltering && (
              <Button
                size="sm"
                variant="outline"
                onClick={resetFilters}
                className="self-end md:self-auto"
              >
                Reset filters
              </Button>
            )}
          </div>

          {academicYears.length > 1 && (
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                Academic year
              </span>
              <button
                type="button"
                onClick={() => setYearFilter("")}
                aria-pressed={!yearFilter}
                className={cn(
                  "focus-ring inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-all",
                  !yearFilter
                    ? "rainbow-bg border-transparent text-white shadow-[0_10px_24px_-16px_rgba(15,40,69,0.55)]"
                    : "border-border/70 bg-card/70 text-muted-foreground hover:border-primary/30 hover:text-foreground"
                )}
              >
                All years
              </button>
              {academicYears.map((year) => {
                const isActive = yearFilter === year;
                return (
                  <button
                    key={year}
                    type="button"
                    onClick={() => setYearFilter(isActive ? "" : year)}
                    aria-pressed={isActive}
                    className={cn(
                      "focus-ring inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-all",
                      isActive
                        ? "rainbow-bg border-transparent text-white shadow-[0_10px_24px_-16px_rgba(15,40,69,0.55)]"
                        : "border-border/70 bg-card/70 text-muted-foreground hover:border-primary/30 hover:text-foreground"
                    )}
                  >
                    {year}
                  </button>
                );
              })}
            </div>
          )}

          <p className="text-sm text-muted-foreground">
            Showing {visibleClasses.length} of {classes.length} class
            {classes.length !== 1 ? "es" : ""}
            {isFiltering ? " (filtered)" : ""}.
          </p>

          {visibleClasses.length === 0 ? (
            <div className="rounded-[24px] border border-dashed border-border/70 bg-card/60 p-8 text-center shadow-[0_18px_46px_-34px_rgba(15,40,69,0.3)] dark:bg-card/80">
              <p className="text-sm font-medium text-foreground">
                No classes match your filters.
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                Try a different search term, clear the academic-year filter, or reset everything.
              </p>
              <Button
                size="sm"
                variant="outline"
                onClick={resetFilters}
                className="mt-4"
              >
                Reset filters
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
                  {/* Floating Edit button overlay — does not interfere with the card link */}
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
            <Button
              type="submit"
              form="class-form"
              disabled={isSubmitting}
            >
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
