"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft } from "lucide-react";
import type {
  CreateTeacherRequest,
  TeacherMutationResponse,
} from "@/lib/types/teacher";

export default function CreateTeacherPage(): React.ReactElement {
  const router = useRouter();
  const [name, setName] = React.useState("");
  const [phone, setPhone] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string>>({});

  const validate = (): boolean => {
    const nextErrors: Record<string, string> = {};

    if (!name.trim()) nextErrors.name = "Teacher name is required.";
    if (!/^\d{10}$/.test(phone)) nextErrors.phone = "Phone number must be exactly 10 digits.";
    if (!email.trim()) nextErrors.email = "Email is required.";
    if (email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      nextErrors.email = "Enter a valid email address.";
    }
    if (password.length < 8) nextErrors.password = "Password must be at least 8 characters.";

    setFieldErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError("");

    if (!validate()) return;

    setIsSubmitting(true);
    try {
      const body: CreateTeacherRequest = {
        name: name.trim(),
        phone,
        email: email.trim().toLowerCase(),
        password,
      };
      const result = await apiPost<TeacherMutationResponse>(
        API_ENDPOINTS.teachers,
        body
      );
      router.push(`/admin/teachers/${result.teacherId}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create teacher.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Add Teacher"
        description="Create a teacher account with temporary credentials so assignments can begin immediately."
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
      />

      <PageSection>
        <CardContent className="p-0">
          <form onSubmit={handleSubmit} className="max-w-2xl space-y-4">
            <Input
              label="Teacher Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.name}
              placeholder="Enter teacher's full name"
            />
            <Input
              label="Phone Number"
              value={phone}
              onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 10))}
              disabled={isSubmitting}
              error={fieldErrors.phone}
              placeholder="10-digit phone number"
              inputMode="numeric"
            />
            <Input
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.email}
              placeholder="teacher@example.com"
            />
            <Input
              label="Temporary Password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.password}
              placeholder="At least 8 characters"
            />

            <StatusBanner variant="info">
              Teachers can later rotate this password with the existing password reset flow.
            </StatusBanner>

            {error && <StatusBanner variant="error">{error}</StatusBanner>}

            <div className="flex gap-2 pt-2">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? <Spinner size="sm" /> : "Create Teacher"}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push("/admin/teachers")}
                disabled={isSubmitting}
              >
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </PageSection>
    </PageShell>
  );
}
