"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { EmptyState } from "@/components/shared/empty-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { BookOpen, Mail, Phone, UserCircle2 } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import type { TeacherProfile, TeacherAssignment } from "@/lib/types/teacher";
import { EnableNotificationsButton } from "@/components/push/EnableNotificationsButton";

export default function TeacherProfilePage(): React.ReactElement {
  const { user, isLoading: isAuthLoading } = useAuth();

  const [profile, setProfile] = React.useState<TeacherProfile | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const fetchProfile = React.useCallback(async (): Promise<void> => {
    if (!user?.userId) return;
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<TeacherProfile>(
        `${API_ENDPOINTS.teachers}/${user.userId}`
      );
      setProfile(data);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load your profile."
      );
    } finally {
      setIsLoading(false);
    }
  }, [user?.userId]);

  React.useEffect(() => {
    if (isAuthLoading) return;
    if (!user?.userId) return;
    void fetchProfile();
  }, [fetchProfile, isAuthLoading, user?.userId]);

  // Group assignments by class for display. Handles the missing-profile
  // case (still loading, or the detail fetch failed) by returning an empty
  // list — the assignments card will show its own empty state.
  const groupedByClass = React.useMemo(() => {
    const map = new Map<
      string,
      {
        classId: string;
        className: string;
        section: string;
        subjects: string[];
        isClassTeacher: boolean;
      }
    >();
    const assignments: TeacherAssignment[] = profile?.assignments ?? [];
    for (const a of assignments) {
      const existing = map.get(a.classId);
      if (existing) {
        existing.subjects.push(a.subject);
        existing.isClassTeacher = existing.isClassTeacher || a.isClassTeacher;
      } else {
        map.set(a.classId, {
          classId: a.classId,
          className: a.className,
          section: a.section,
          subjects: [a.subject],
          isClassTeacher: a.isClassTeacher,
        });
      }
    }
    return Array.from(map.values());
  }, [profile?.assignments]);

  const assignmentCount = profile?.assignments.length ?? 0;

  // Prefer the authoritative server profile once loaded; fall back to the
  // name embedded in the JWT so the teacher's name never renders as blank
  // (the bug this page was updated to fix). `user.name` is available
  // synchronously after auth hydration.
  const displayName = profile?.name?.trim() || user?.name || "";
  const displayRole = profile?.role ?? user?.role ?? "Teacher";

  // Only short-circuit to the full-page spinner on the very first load when
  // we truly have nothing to show. Once `user` is hydrated, we can render the
  // header skeleton with the JWT name even before the detail fetch resolves.
  if (isAuthLoading && !user) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  // Hard-fail path: only surface the fatal error page if we have neither a
  // cached profile nor a JWT fallback to fall back on. In practice this is
  // "unauthenticated with a broken API" — extremely unlikely but safe.
  if (error && !profile && !user) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState title="Error" message={error} onRetry={fetchProfile} />
      </div>
    );
  }

  return (
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title="My Profile"
        description="Review your account details and active class assignments."
        icon={<BookOpen className="h-6 w-6" aria-hidden="true" />}
        stats={[
          { label: "Classes", value: groupedByClass.length.toString() },
          { label: "Assignments", value: assignmentCount.toString() },
        ]}
      />

      <PageSection className="space-y-5">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-end">
          <EnableNotificationsButton />
        </div>
        {/* Teacher identity card — always renders, even while the profile
            detail fetch is in flight, by falling back to JWT-derived values.
            This is the fix for the "teacher name not displayed" bug. */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-lg">Account details</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
              <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
                <UserCircle2 className="h-8 w-8" aria-hidden="true" />
              </div>
              <div className="min-w-0 flex-1 space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="truncate text-xl font-semibold text-foreground">
                    {displayName || (isLoading ? "Loading…" : "Unknown teacher")}
                  </h2>
                  <Badge variant="secondary">{displayRole}</Badge>
                  {profile && !profile.isActive ? (
                    <Badge variant="destructive">Inactive</Badge>
                  ) : null}
                </div>
                <div className="flex flex-col gap-1 text-sm text-muted-foreground">
                  {profile?.email ? (
                    <span className="inline-flex items-center gap-2">
                      <Mail className="h-4 w-4" aria-hidden="true" />
                      <span className="truncate">{profile.email}</span>
                    </span>
                  ) : null}
                  {profile?.phone ? (
                    <span className="inline-flex items-center gap-2">
                      <Phone className="h-4 w-4" aria-hidden="true" />
                      <span className="truncate">{profile.phone}</span>
                    </span>
                  ) : null}
                  {/* If the profile detail fetch errored but we still have a
                      JWT-derived name, surface the error inline rather than
                      replacing the whole page — the teacher can still see
                      their identity and retry. */}
                  {error && !profile ? (
                    <span className="text-destructive">{error}</span>
                  ) : null}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {isLoading && !profile ? (
          <div className="flex min-h-48 items-center justify-center">
            <Spinner size="lg" />
          </div>
        ) : assignmentCount === 0 ? (
          <EmptyState
            title="No assignments yet"
            description="You have not been assigned to any classes. Contact your admin for assignments."
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
              {groupedByClass.length} class
              {groupedByClass.length !== 1 ? "es" : ""},{" "}
              {assignmentCount} assignment
              {assignmentCount !== 1 ? "s" : ""}
            </p>

            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {groupedByClass.map((group) => (
                <Card key={group.classId}>
                  <CardHeader className="pb-2">
                    <div className="flex items-start justify-between gap-3">
                      <CardTitle className="text-lg">
                        {group.className}
                        {group.section ? ` ${group.section}` : ""}
                      </CardTitle>
                      {group.isClassTeacher && (
                        <Badge variant="default">Class teacher</Badge>
                      )}
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="flex flex-wrap gap-2">
                      {group.subjects.map((subject) => (
                        <Badge key={subject} variant="secondary">
                          {subject}
                        </Badge>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </>
        )}
      </PageSection>
    </PageShell>
  );
}
