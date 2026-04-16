"use client";

import * as React from "react";
import { ApiError, apiGet, apiPost, apiPut } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { formatNoticeAudienceDetails, formatNoticeAudienceLabel } from "@/lib/notice-targeting";
import type {
  CreateNoticeRequest,
  CreateNoticeResponse,
  NoticeItem,
  PublishNoticeResponse,
} from "@/lib/types/notice";
import type { ClassItem } from "@/lib/types/student";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import {
  PageHeader,
  PageSection,
  PageShell,
} from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { AttachmentUploader, type UploadedFile } from "@/components/shared/attachment-uploader";
import { AttachmentList } from "@/components/shared/attachment-list";
import { Bell, Check, Plus, Send } from "lucide-react";

type NoticeTargetAudience = "All" | "Class" | "Section";

interface ClassGroupOption {
  key: string;
  name: string;
  academicYear: string;
  sections: ClassItem[];
}

function buildClassGroupKey(classItem: Pick<ClassItem, "name" | "academicYear">): string {
  return `${classItem.name}::${classItem.academicYear}`;
}

function formatClassGroupLabel(group: ClassGroupOption): string {
  return group.academicYear
    ? `Class ${group.name} • ${group.academicYear}`
    : `Class ${group.name}`;
}

function areSameIds(left: string[], right: string[]): boolean {
  if (left.length !== right.length) {
    return false;
  }

  return left.every((value, index) => value === right[index]);
}

export default function AdminNoticesPage(): React.ReactElement {
  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [classLoadError, setClassLoadError] = React.useState("");
  const [notices, setNotices] = React.useState<NoticeItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const [showCreateForm, setShowCreateForm] = React.useState(false);
  const [createTitle, setCreateTitle] = React.useState("");
  const [createBody, setCreateBody] = React.useState("");
  const [createTargetAudience, setCreateTargetAudience] =
    React.useState<NoticeTargetAudience>("All");
  const [createClassGroupKey, setCreateClassGroupKey] = React.useState("");
  const [createTargetSectionIds, setCreateTargetSectionIds] = React.useState<string[]>([]);
  const [createExpiresAt, setCreateExpiresAt] = React.useState("");
  const [createError, setCreateError] = React.useState("");
  const [isCreating, setIsCreating] = React.useState(false);

  const [isPublishing, setIsPublishing] = React.useState<string | null>(null);
  const [successMessage, setSuccessMessage] = React.useState("");

  const [newNoticeId, setNewNoticeId] = React.useState<string | null>(null);
  const [newNoticeAttachments, setNewNoticeAttachments] = React.useState<UploadedFile[]>([]);

  const classGroups = React.useMemo(() => {
    const grouped = new Map<string, ClassGroupOption>();

    classes.forEach((classItem) => {
      const key = buildClassGroupKey(classItem);
      const existing = grouped.get(key);
      if (existing) {
        existing.sections.push(classItem);
        return;
      }

      grouped.set(key, {
        key,
        name: classItem.name,
        academicYear: classItem.academicYear,
        sections: [classItem],
      });
    });

    return Array.from(grouped.values())
      .map((group) => ({
        ...group,
        sections: [...group.sections].sort((left, right) =>
          left.section.localeCompare(right.section)
        ),
      }))
      .sort((left, right) => {
        const byName = left.name.localeCompare(right.name);
        if (byName !== 0) {
          return byName;
        }

        return left.academicYear.localeCompare(right.academicYear);
      });
  }, [classes]);

  const selectedClassGroup = React.useMemo(
    () => classGroups.find((group) => group.key === createClassGroupKey) ?? null,
    [classGroups, createClassGroupKey]
  );

  const selectedTargetClassIds = React.useMemo(() => {
    if (createTargetAudience === "All" || !selectedClassGroup) {
      return [];
    }

    if (createTargetAudience === "Class") {
      return selectedClassGroup.sections.map((section) => section.id);
    }

    return selectedClassGroup.sections
      .filter((section) => createTargetSectionIds.includes(section.id))
      .map((section) => section.id);
  }, [createTargetAudience, createTargetSectionIds, selectedClassGroup]);

  const selectedSections = React.useMemo(() => {
    if (!selectedClassGroup) {
      return [];
    }

    if (createTargetAudience === "Class") {
      return selectedClassGroup.sections;
    }

    if (createTargetAudience === "Section") {
      return selectedClassGroup.sections.filter((section) =>
        createTargetSectionIds.includes(section.id)
      );
    }

    return [];
  }, [createTargetAudience, createTargetSectionIds, selectedClassGroup]);

  const audiencePreview = React.useMemo(() => {
    if (createTargetAudience === "All") {
      return {
        title: "Whole School",
        detail:
          "This notice will be available to the full school audience after you publish it.",
      };
    }

    if (!selectedClassGroup) {
      return {
        title: "Choose a class",
        detail:
          "Select the class first so the system can show the right sections for this notice.",
      };
    }

    const selectedSectionLabels = selectedSections.map((section) => section.section);
    const selectedStudentCount = selectedSections.reduce(
      (count, section) => count + section.studentCount,
      0
    );

    if (createTargetAudience === "Class") {
      return {
        title: `${formatClassGroupLabel(selectedClassGroup)} • All Sections`,
        detail: `Sections ${selectedSectionLabels.join(", ")} are included. These sections currently have ${selectedStudentCount} student${selectedStudentCount === 1 ? "" : "s"}.`,
      };
    }

    if (selectedSections.length === 0) {
      return {
        title: `${formatClassGroupLabel(selectedClassGroup)} • Specific Sections`,
        detail:
          "Pick one or more sections so the notice reaches only the intended audience.",
      };
    }

    return {
      title: `${formatClassGroupLabel(selectedClassGroup)} • ${selectedSections.length} Section${selectedSections.length === 1 ? "" : "s"}`,
      detail: `Selected sections: ${selectedSectionLabels.join(", ")}. These sections currently have ${selectedStudentCount} student${selectedStudentCount === 1 ? "" : "s"}.`,
    };
  }, [createTargetAudience, selectedClassGroup, selectedSections]);

  const fetchClasses = React.useCallback(async () => {
    setClassLoadError("");
    try {
      const data = await apiGet<ClassItem[]>(API_ENDPOINTS.classes);
      setClasses(data);
    } catch {
      setClassLoadError("Failed to load classes for notice targeting.");
    }
  }, []);

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
    void fetchNotices();
  }, [fetchNotices]);

  React.useEffect(() => {
    void fetchClasses();
  }, [fetchClasses]);

  React.useEffect(() => {
    if (createTargetAudience === "All") {
      if (createClassGroupKey) {
        setCreateClassGroupKey("");
      }
      if (createTargetSectionIds.length > 0) {
        setCreateTargetSectionIds([]);
      }
      return;
    }

    if (!selectedClassGroup) {
      if (createTargetSectionIds.length > 0) {
        setCreateTargetSectionIds([]);
      }
      return;
    }

    const validSectionIds = selectedClassGroup.sections.map((section) => section.id);
    const nextSectionIds = createTargetSectionIds.filter((sectionId) =>
      validSectionIds.includes(sectionId)
    );

    if (
      createTargetAudience === "Section" &&
      nextSectionIds.length === 0 &&
      selectedClassGroup.sections.length === 1
    ) {
      const [onlySection] = selectedClassGroup.sections;
      if (onlySection) {
        setCreateTargetSectionIds([onlySection.id]);
      }
      return;
    }

    if (!areSameIds(createTargetSectionIds, nextSectionIds)) {
      setCreateTargetSectionIds(nextSectionIds);
    }
  }, [
    createClassGroupKey,
    createTargetAudience,
    createTargetSectionIds,
    selectedClassGroup,
  ]);

  const resetCreateForm = React.useCallback(() => {
    setCreateTitle("");
    setCreateBody("");
    setCreateTargetAudience("All");
    setCreateClassGroupKey("");
    setCreateTargetSectionIds([]);
    setCreateExpiresAt("");
    setCreateError("");
  }, []);

  const handleCreate = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setCreateError("");
    setSuccessMessage("");

    if (!createTitle.trim() || !createBody.trim()) {
      setCreateError("Title and body are required.");
      return;
    }

    if (createTargetAudience !== "All" && classLoadError) {
      setCreateError("Class list is unavailable right now. Refresh the page and try again.");
      return;
    }

    if (createTargetAudience !== "All" && !selectedClassGroup) {
      setCreateError("Select a class before targeting the notice.");
      return;
    }

    if (createTargetAudience === "Section" && selectedTargetClassIds.length === 0) {
      setCreateError("Select at least one section for this notice.");
      return;
    }

    const body: CreateNoticeRequest = {
      title: createTitle.trim(),
      body: createBody.trim(),
      targetAudience: createTargetAudience,
      targetClassIds:
        createTargetAudience === "All" ? null : selectedTargetClassIds,
      expiresAt: createExpiresAt || null,
    };

    setIsCreating(true);
    try {
      const response = await apiPost<CreateNoticeResponse>(API_ENDPOINTS.notices, body);
      setSuccessMessage(response.message);
      setShowCreateForm(false);
      resetCreateForm();
      setNewNoticeId(response.noticeId);
      setNewNoticeAttachments([]);
      void fetchNotices();
    } catch (err) {
      setCreateError(
        err instanceof ApiError ? err.message : "Failed to create notice."
      );
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
      void fetchNotices();
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to publish notice."
      );
    } finally {
      setIsPublishing(null);
    }
  };

  const toggleSection = (sectionId: string): void => {
    setCreateTargetSectionIds((current) =>
      current.includes(sectionId)
        ? current.filter((value) => value !== sectionId)
        : [...current, sectionId]
    );
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

  const drafts = notices.filter((notice) => !notice.isPublished);
  const published = notices.filter((notice) => notice.isPublished);

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Notices"
        description="Draft, attach, and publish announcements with clear audience targeting."
        icon={<Bell className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <Button
            onClick={() => {
              setShowCreateForm((current) => !current);
              setCreateError("");
              setSuccessMessage("");
            }}
            size="sm"
          >
            <Plus className="h-4 w-4" />
            New Notice
          </Button>
        )}
        stats={[
          { label: "Drafts", value: drafts.length.toString() },
          { label: "Published", value: published.length.toString() },
        ]}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}

      {newNoticeId && (
        <PageSection className="space-y-4">
          <div>
            <h3 className="text-lg font-semibold">Attach Files to Notice</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              Optionally attach images or PDFs to the draft notice before publishing.
            </p>
          </div>
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
              void fetchNotices();
            }}
          >
            Done
          </Button>
        </PageSection>
      )}

      {showCreateForm && (
        <PageSection>
          <form onSubmit={handleCreate} className="space-y-5">
            <h3 className="text-lg font-semibold">Create Notice</h3>

            <Input
              id="createTitle"
              label="Title"
              placeholder="Notice title"
              value={createTitle}
              onChange={(e) => setCreateTitle(e.target.value)}
              disabled={isCreating}
            />

            <Textarea
              id="createBody"
              label="Body"
              placeholder="Notice content..."
              value={createBody}
              onChange={(e) => setCreateBody(e.target.value)}
              disabled={isCreating}
              rows={5}
            />

            <div className="grid gap-3 md:grid-cols-2">
              <Select
                id="createTargetAudience"
                label="Target Audience"
                value={createTargetAudience}
                onChange={(e) => {
                  setCreateTargetAudience(e.target.value as NoticeTargetAudience);
                  setCreateError("");
                }}
                disabled={isCreating}
                hint="Choose whether this notice goes to everyone, one class across all sections, or only selected sections."
              >
                <option value="All">Whole School</option>
                <option value="Class">Class (All Sections)</option>
                <option value="Section">Specific Sections</option>
              </Select>

              <Input
                id="createExpiresAt"
                label="Expires At (optional)"
                type="datetime-local"
                value={createExpiresAt}
                onChange={(e) => setCreateExpiresAt(e.target.value)}
                disabled={isCreating}
              />
            </div>

            {createTargetAudience !== "All" && (
              <div className="space-y-4 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_20px_50px_-40px_rgba(15,23,42,0.42)] dark:bg-card/88">
                <div className="space-y-1">
                  <p className="text-sm font-medium text-foreground">
                    Class Targeting
                  </p>
                  <p className="text-sm text-muted-foreground">
                    Start by choosing the class group, then either include every
                    section or hand-pick the sections that should receive the notice.
                  </p>
                </div>

                {classLoadError && (
                  <StatusBanner variant="warning">{classLoadError}</StatusBanner>
                )}

                {!classLoadError && classes.length === 0 && (
                  <StatusBanner variant="warning">
                    No classes are available yet. Create classes first to send targeted notices.
                  </StatusBanner>
                )}

                <Select
                  id="createClassGroupKey"
                  label="Class"
                  value={createClassGroupKey}
                  onChange={(e) => {
                    setCreateClassGroupKey(e.target.value);
                    setCreateError("");
                  }}
                  disabled={isCreating || !!classLoadError || classes.length === 0}
                  hint={
                    classLoadError
                      ? "Reload the page to retry loading classes."
                      : "This groups all sections from the same class and academic year."
                  }
                >
                  <option value="" disabled>
                    Select a class
                  </option>
                  {classGroups.map((group) => (
                    <option key={group.key} value={group.key}>
                      {formatClassGroupLabel(group)}
                    </option>
                  ))}
                </Select>

                {selectedClassGroup && (
                  <div className="rounded-[22px] border border-border/70 bg-card/60 px-4 py-3 shadow-[0_12px_30px_-28px_rgba(15,23,42,0.38)]">
                    <p className="text-sm font-medium text-foreground">
                      {formatClassGroupLabel(selectedClassGroup)}
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {selectedClassGroup.sections.map((section) => {
                        const isSelected =
                          createTargetAudience === "Class" ||
                          createTargetSectionIds.includes(section.id);

                        return (
                          <Badge
                            key={section.id}
                            variant={isSelected ? "default" : "outline"}
                          >
                            Section {section.section}
                            {section.studentCount > 0
                              ? ` • ${section.studentCount} students`
                              : ""}
                          </Badge>
                        );
                      })}
                    </div>
                    <p className="mt-3 text-sm text-muted-foreground">
                      {createTargetAudience === "Class"
                        ? `This notice will go to every section in ${formatClassGroupLabel(selectedClassGroup)}.`
                        : selectedTargetClassIds.length > 0
                          ? `${selectedTargetClassIds.length} section${selectedTargetClassIds.length === 1 ? "" : "s"} selected for this notice.`
                          : "Pick the specific sections that should receive this notice."}
                    </p>
                  </div>
                )}

                {createTargetAudience === "Section" && selectedClassGroup && (
                  <div className="space-y-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium text-foreground">
                        Sections
                      </p>
                      <div className="flex gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            setCreateTargetSectionIds(
                              selectedClassGroup.sections.map((section) => section.id)
                            )
                          }
                          disabled={isCreating}
                        >
                          Select All
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() => setCreateTargetSectionIds([])}
                          disabled={isCreating || createTargetSectionIds.length === 0}
                        >
                          Clear
                        </Button>
                      </div>
                    </div>

                    <div className="grid gap-3 sm:grid-cols-2">
                      {selectedClassGroup.sections.map((section) => {
                        const isSelected = createTargetSectionIds.includes(section.id);

                        return (
                          <button
                            key={section.id}
                            type="button"
                            onClick={() => toggleSection(section.id)}
                            aria-pressed={isSelected}
                            className={`flex items-center justify-between rounded-[22px] border px-4 py-3 text-left transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                              isSelected
                                ? "border-primary bg-primary/6"
                                : "border-border/70 bg-card/60 hover:border-primary/20 hover:bg-card"
                            }`}
                          >
                            <div>
                              <p className="font-medium text-foreground">
                                Section {section.section}
                              </p>
                              <p className="text-sm text-muted-foreground">
                                {section.studentCount} student
                                {section.studentCount === 1 ? "" : "s"}
                              </p>
                            </div>
                            {isSelected && (
                              <Check className="h-4 w-4 text-primary" aria-hidden="true" />
                            )}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="rounded-[24px] border border-dashed border-border/80 bg-muted/30 p-4">
              <p className="text-sm font-medium text-foreground">
                Audience Preview
              </p>
              <p className="mt-2 text-sm font-semibold text-foreground">
                {audiencePreview.title}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                {audiencePreview.detail}
              </p>
            </div>

            {createError && (
              <StatusBanner variant="error">{createError}</StatusBanner>
            )}

            <div className="flex gap-2">
              <Button type="submit" size="sm" disabled={isCreating}>
                {isCreating ? <Spinner size="sm" /> : "Create Draft"}
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setShowCreateForm(false);
                  setCreateError("");
                }}
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
        <PageSection className="space-y-6">
          {drafts.length > 0 && (
            <div className="space-y-3">
              <h2 className="text-lg font-semibold">Drafts</h2>
              {drafts.map((notice) => (
                <Card key={notice.noticeId}>
                  <CardHeader className="pb-2">
                    <div className="flex items-start justify-between gap-2">
                      <CardTitle className="text-lg">{notice.title}</CardTitle>
                      <div className="flex flex-wrap items-center justify-end gap-2">
                        <Badge variant="outline">Draft</Badge>
                        <Badge variant="secondary">
                          {formatNoticeAudienceLabel(notice)}
                        </Badge>
                        <Button
                          size="sm"
                          onClick={() => void handlePublish(notice.noticeId)}
                          disabled={isPublishing === notice.noticeId}
                        >
                          {isPublishing === notice.noticeId ? (
                            <Spinner size="sm" />
                          ) : (
                            <>
                              <Send className="h-3 w-3" />
                              Publish
                            </>
                          )}
                        </Button>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <p className="whitespace-pre-wrap text-sm">{notice.body}</p>
                    <div className="mt-3">
                      <AttachmentList entityId={notice.noticeId} entityType="notice" />
                    </div>
                    {formatNoticeAudienceDetails(notice) && (
                      <p className="mt-2 text-xs text-muted-foreground">
                        {formatNoticeAudienceDetails(notice)}
                      </p>
                    )}
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
                      <div className="flex flex-wrap items-center justify-end gap-2">
                        <Badge>Published</Badge>
                        <Badge variant="secondary">
                          {formatNoticeAudienceLabel(notice)}
                        </Badge>
                      </div>
                    </div>
                    {notice.publishedAt && (
                      <p className="text-xs text-muted-foreground">
                        Published: {formatDate(notice.publishedAt)}
                      </p>
                    )}
                  </CardHeader>
                  <CardContent>
                    <p className="whitespace-pre-wrap text-sm">{notice.body}</p>
                    <div className="mt-3">
                      <AttachmentList entityId={notice.noticeId} entityType="notice" />
                    </div>
                    {formatNoticeAudienceDetails(notice) && (
                      <p className="mt-2 text-xs text-muted-foreground">
                        {formatNoticeAudienceDetails(notice)}
                      </p>
                    )}
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
        </PageSection>
      )}
    </PageShell>
  );
}
