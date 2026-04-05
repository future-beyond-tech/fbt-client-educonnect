"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet, apiPut, apiDelete } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ErrorState } from "@/components/shared/error-state";
import { ParentLinkList } from "@/components/shared/parent-link-list";
import {
  ArrowLeft,
  Pencil,
  UserMinus,
  Link as LinkIcon,
} from "lucide-react";
import type { StudentDetail, MutationResponse } from "@/lib/types/student";

export default function AdminStudentDetailPage(): React.ReactElement {
  const params = useParams();
  const router = useRouter();
  const studentId = params.id as string;

  const [student, setStudent] = React.useState<StudentDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [isDeactivating, setIsDeactivating] = React.useState(false);
  const [isUnlinking, setIsUnlinking] = React.useState<string | null>(null);
  const [successMessage, setSuccessMessage] = React.useState("");

  const fetchStudent = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<StudentDetail>(
        `${API_ENDPOINTS.students}/${studentId}`
      );
      setStudent(data);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load student."
      );
    } finally {
      setIsLoading(false);
    }
  }, [studentId]);

  React.useEffect(() => {
    fetchStudent();
  }, [fetchStudent]);

  const handleDeactivate = async (): Promise<void> => {
    if (!confirm("Are you sure you want to deactivate this student?")) return;

    setIsDeactivating(true);
    setSuccessMessage("");
    try {
      const result = await apiPut<MutationResponse>(
        `${API_ENDPOINTS.students}/${studentId}/deactivate`
      );
      setSuccessMessage(result.message);
      fetchStudent();
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to deactivate student."
      );
    } finally {
      setIsDeactivating(false);
    }
  };

  const handleUnlink = async (
    linkId: string,
    parentName: string
  ): Promise<void> => {
    if (!confirm(`Unlink ${parentName} from this student?`)) return;

    setIsUnlinking(linkId);
    setSuccessMessage("");
    try {
      await apiDelete<MutationResponse>(
        `${API_ENDPOINTS.students}/${studentId}/parent-links/${linkId}`
      );
      setSuccessMessage("Parent unlinked successfully.");
      fetchStudent();
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to unlink parent."
      );
    } finally {
      setIsUnlinking(null);
    }
  };

  const formatDate = (dateStr: string): string => {
    return new Date(dateStr).toLocaleDateString("en-IN", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  if (isLoading) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Spinner size="lg" />
      </div>
    );
  }

  if (error && !student) {
    return (
      <div className="p-4 md:p-8">
        <ErrorState title="Error" message={error} onRetry={fetchStudent} />
      </div>
    );
  }

  if (!student) return <></>;

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push("/admin/students")}
          aria-label="Back to students"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <h1 className="text-3xl font-bold tracking-tight">
              {student.name}
            </h1>
            {!student.isActive && <Badge variant="destructive">Inactive</Badge>}
          </div>
          <p className="text-muted-foreground">
            Roll: {student.rollNumber} &middot; {student.className}
            {student.section ? ` ${student.section}` : ""}
          </p>
        </div>
      </div>

      {successMessage && (
        <div className="rounded-md bg-green-50 p-3 text-sm text-green-800 dark:bg-green-950 dark:text-green-200">
          {successMessage}
        </div>
      )}

      {error && (
        <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => router.push(`/admin/students/${studentId}/edit`)}
        >
          <Pencil className="mr-1 h-4 w-4" />
          Edit
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() =>
            router.push(`/admin/students/${studentId}/link-parent`)
          }
        >
          <LinkIcon className="mr-1 h-4 w-4" />
          Link Parent
        </Button>
        {student.isActive && (
          <Button
            variant="destructive"
            size="sm"
            onClick={handleDeactivate}
            disabled={isDeactivating}
          >
            {isDeactivating ? (
              <Spinner size="sm" />
            ) : (
              <>
                <UserMinus className="mr-1 h-4 w-4" />
                Deactivate
              </>
            )}
          </Button>
        )}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Personal Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Full Name</span>
              <span className="text-sm font-medium">{student.name}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Roll Number</span>
              <span className="text-sm font-medium">{student.rollNumber}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Class</span>
              <span className="text-sm font-medium">
                {student.className}
                {student.section ? ` ${student.section}` : ""}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">
                Academic Year
              </span>
              <span className="text-sm font-medium">
                {student.academicYear || "—"}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">
                Date of Birth
              </span>
              <span className="text-sm font-medium">
                {student.dateOfBirth
                  ? formatDate(student.dateOfBirth)
                  : "—"}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-muted-foreground">Enrolled</span>
              <span className="text-sm font-medium">
                {formatDate(student.createdAt)}
              </span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Linked Parents</CardTitle>
          </CardHeader>
          <CardContent>
            <ParentLinkList
              links={student.parentLinks}
              canUnlink
              onUnlink={handleUnlink}
              isUnlinking={isUnlinking}
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
