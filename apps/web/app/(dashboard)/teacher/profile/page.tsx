"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { EmptyState } from "@/components/shared/empty-state";
import { BookOpen } from "lucide-react";
import type { TeacherClassItem } from "@/lib/types/teacher";

export default function TeacherProfilePage(): React.ReactElement {
  const [assignments, setAssignments] = React.useState<TeacherClassItem[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const fetchAssignments = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<TeacherClassItem[]>(
        API_ENDPOINTS.teachersMyClasses
      );
      setAssignments(data);
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : "Failed to load your assignments."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    fetchAssignments();
  }, [fetchAssignments]);

  // Group assignments by class for display
  const groupedByClass = React.useMemo(() => {
    const map = new Map<
      string,
      { classId: string; className: string; section: string; subjects: string[] }
    >();
    for (const a of assignments) {
      const key = a.classId;
      const existing = map.get(key);
      if (existing) {
        existing.subjects.push(a.subject);
      } else {
        map.set(key, {
          classId: a.classId,
          className: a.className,
          section: a.section,
          subjects: [a.subject],
        });
      }
    }
    return Array.from(map.values());
  }, [assignments]);

  if (isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState
          title="Error"
          message={error}
          onRetry={fetchAssignments}
        />
      </div>
    );
  }

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">My Profile</h1>
        <p className="text-muted-foreground">
          Your class and subject assignments.
        </p>
      </div>

      {assignments.length === 0 ? (
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
            {assignments.length} assignment
            {assignments.length !== 1 ? "s" : ""}
          </p>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {groupedByClass.map((group) => (
              <Card key={group.classId}>
                <CardHeader className="pb-2">
                  <CardTitle className="text-lg">
                    {group.className}
                    {group.section ? ` ${group.section}` : ""}
                  </CardTitle>
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
    </div>
  );
}
