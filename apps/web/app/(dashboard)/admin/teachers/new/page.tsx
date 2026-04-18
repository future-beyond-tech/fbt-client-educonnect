"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import {
  isValidJapanPhone,
  JAPAN_PHONE_LOCAL_DIGITS,
  JAPAN_PHONE_VALIDATION_MESSAGE,
  normalizeJapanPhoneInput,
} from "@/lib/phone";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { ClassSelector } from "@/components/shared/class-selector";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { TemporaryCredentialCard } from "@/components/shared/temporary-credential-card";
import { ArrowLeft, Eye, EyeOff } from "lucide-react";
import { cn } from "@/lib/utils";
import type {
  CreateTeacherRequest,
  SubjectItem,
  TeacherMutationResponse,
} from "@/lib/types/teacher";
import type { ClassItem } from "@/lib/types/student";

export default function CreateTeacherPage(): React.ReactElement {
  const router = useRouter();
  const [name, setName] = React.useState("");
  const [phone, setPhone] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [showPassword, setShowPassword] = React.useState(false);
  const [role, setRole] = React.useState<"Teacher" | "Admin">("Teacher");
  const passwordFieldId = React.useId();
  const passwordErrorId = `${passwordFieldId}-error`;

  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [subjects, setSubjects] = React.useState<SubjectItem[]>([]);
  const [assignClassId, setAssignClassId] = React.useState("");
  const [assignSubject, setAssignSubject] = React.useState("");
  const [assignIsClassTeacher, setAssignIsClassTeacher] = React.useState(false);

  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string>>({});
  const [catalogLoading, setCatalogLoading] = React.useState(true);
  const [catalogError, setCatalogError] = React.useState("");
  const [createdTeacher, setCreatedTeacher] = React.useState<{
    teacherId: string;
    name: string;
    role: "Teacher" | "Admin";
    temporaryPassword: string;
  } | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    (async (): Promise<void> => {
      setCatalogError("");
      try {
        const [classData, subjectData] = await Promise.all([
          apiGet<ClassItem[]>(API_ENDPOINTS.classes),
          apiGet<SubjectItem[]>(API_ENDPOINTS.subjects),
        ]);
        if (!cancelled) {
          setClasses(classData);
          setSubjects(subjectData);
        }
      } catch (err) {
        if (!cancelled) {
          setCatalogError(
            err instanceof ApiError ? err.message : "Failed to load classes and subjects."
          );
        }
      } finally {
        if (!cancelled) setCatalogLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const validate = (): boolean => {
    const nextErrors: Record<string, string> = {};

    if (!name.trim()) nextErrors.name = "Name is required.";
    if (!isValidJapanPhone(phone)) nextErrors.phone = JAPAN_PHONE_VALIDATION_MESSAGE;
    if (!email.trim()) nextErrors.email = "Email is required.";
    if (email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      nextErrors.email = "Enter a valid email address.";
    }
    if (password.length < 8) nextErrors.password = "Password must be at least 8 characters.";

    if (role === "Teacher") {
      const hasClass = Boolean(assignClassId);
      const hasSubject = Boolean(assignSubject.trim());
      if (hasClass !== hasSubject) {
        if (hasClass && !hasSubject) {
          nextErrors.subject = "Select a subject when a class is selected.";
        }
        if (!hasClass && hasSubject) {
          nextErrors.classId = "Select a class when a subject is selected.";
        }
      }
    }

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
        role,
        ...(role === "Teacher" && assignClassId && assignSubject.trim()
          ? {
              classId: assignClassId,
              subject: assignSubject.trim(),
              isClassTeacher: assignIsClassTeacher,
            }
          : {}),
      };
      const result = await apiPost<TeacherMutationResponse>(
        API_ENDPOINTS.teachers,
        body
      );
      // Hold the admin on-screen so they can capture the temporary password —
      // the API never echoes it back again. They'll navigate to the profile
      // from the confirmation card.
      if (result.teacherId) {
        setCreatedTeacher({
          teacherId: result.teacherId,
          name: body.name,
          role,
          temporaryPassword: result.temporaryPassword ?? password,
        });
      } else {
        router.push("/admin/teachers");
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to create staff account.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const assignmentBlocked =
    !catalogLoading && (classes.length === 0 || subjects.length === 0);

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Add Staff"
        description="Create a teacher or admin account so the school can keep at least two active administrators when needed."
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push("/admin/teachers")}
            aria-label="Back to staff"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Staff
          </Button>
        )}
      />

      <PageSection>
        <CardContent className="p-0">
          {createdTeacher ? (
            <div className="max-w-2xl space-y-4">
              <StatusBanner variant="success" title={`${createdTeacher.role} account created`}>
                <p>
                  {createdTeacher.name} can now sign in. They will be required
                  to change this temporary password on their first login.
                </p>
              </StatusBanner>
              <TemporaryCredentialCard
                label="Temporary Password"
                value={createdTeacher.temporaryPassword}
                recipient={createdTeacher.name}
                helperText="Staff must replace this password on first login; the change-password flow will revoke any existing sessions."
              />
              <div className="flex flex-wrap gap-2 pt-2">
                <Button
                  type="button"
                  onClick={() =>
                    router.push(`/admin/teachers/${createdTeacher.teacherId}`)
                  }
                >
                  Go to {createdTeacher.role} Profile
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => router.push("/admin/teachers")}
                >
                  Back to Staff List
                </Button>
              </div>
            </div>
          ) : (
          <form onSubmit={handleSubmit} className="max-w-2xl space-y-4">
            <Input
              label={role === "Admin" ? "Admin Name" : "Teacher Name"}
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.name}
              placeholder={role === "Admin" ? "Enter admin's full name" : "Enter teacher's full name"}
            />
            <Select
              id="staffRole"
              label="Role"
              value={role}
              onChange={(e) => {
                const nextRole = e.target.value as "Teacher" | "Admin";
                setRole(nextRole);

                if (nextRole === "Admin") {
                  setAssignClassId("");
                  setAssignSubject("");
                  setAssignIsClassTeacher(false);
                  setFieldErrors((current) => {
                    const next = { ...current };
                    delete next.classId;
                    delete next.subject;
                    return next;
                  });
                }
              }}
              disabled={isSubmitting}
            >
              <option value="Teacher">Teacher</option>
              <option value="Admin">Admin</option>
            </Select>
            <Input
              label="Phone Number"
              value={phone}
              onChange={(e) => setPhone(normalizeJapanPhoneInput(e.target.value))}
              disabled={isSubmitting}
              error={fieldErrors.phone}
              placeholder="11-digit phone number"
              inputMode="numeric"
              maxLength={JAPAN_PHONE_LOCAL_DIGITS}
            />
            <Input
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.email}
              placeholder={role === "Admin" ? "admin@example.com" : "teacher@example.com"}
            />
            <div className="space-y-2">
              <label
                htmlFor={passwordFieldId}
                className="block text-sm font-medium text-foreground"
              >
                Temporary Password
              </label>
              <div className="relative">
                <input
                  id={passwordFieldId}
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="At least 8 characters"
                  autoComplete="new-password"
                  aria-invalid={!!fieldErrors.password}
                  aria-describedby={fieldErrors.password ? passwordErrorId : undefined}
                  className={cn(
                    "focus-ring flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 pr-12 text-sm text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-60",
                    fieldErrors.password &&
                      "border-destructive focus-visible:ring-destructive"
                  )}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((prev) => !prev)}
                  disabled={isSubmitting}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  aria-pressed={showPassword}
                  aria-controls={passwordFieldId}
                  className="focus-ring absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-[20px] text-muted-foreground transition-colors hover:text-foreground disabled:cursor-not-allowed disabled:opacity-60"
                  tabIndex={isSubmitting ? -1 : 0}
                >
                  {showPassword ? (
                    <EyeOff className="h-5 w-5" aria-hidden="true" />
                  ) : (
                    <Eye className="h-5 w-5" aria-hidden="true" />
                  )}
                </button>
              </div>
              {fieldErrors.password && (
                <p
                  id={passwordErrorId}
                  className="text-sm font-medium text-destructive"
                >
                  {fieldErrors.password}
                </p>
              )}
            </div>

            {role === "Teacher" ? (
              <div className="space-y-3 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_16px_40px_-30px_rgba(15,40,69,0.42)] dark:bg-card/88">
                <p className="text-sm font-medium text-foreground">
                  Initial assignment (optional)
                </p>
                <p className="text-xs text-muted-foreground">
                  You can assign a class and subject now, or add them later from the teacher profile.
                </p>
                {catalogLoading ? (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Spinner size="sm" />
                    Loading classes and subjects…
                  </div>
                ) : (
                  <>
                    <ClassSelector
                      classes={classes}
                      value={assignClassId}
                      onChange={setAssignClassId}
                      disabled={isSubmitting || assignmentBlocked}
                      label="Class"
                      error={fieldErrors.classId}
                    />
                    <Select
                      id="createTeacherSubject"
                      label="Subject"
                      value={assignSubject}
                      onChange={(e) => setAssignSubject(e.target.value)}
                      disabled={isSubmitting || assignmentBlocked}
                      error={fieldErrors.subject}
                    >
                      <option value="" disabled>
                        Select a subject
                      </option>
                      {subjects.map((s) => (
                        <option key={s.id} value={s.name}>
                          {s.name}
                        </option>
                      ))}
                    </Select>
                    <label className="flex items-center gap-3 rounded-[20px] border border-border/70 bg-card/74 px-4 py-3 text-sm text-foreground shadow-[0_14px_32px_-26px_rgba(15,40,69,0.4)] dark:bg-card/90">
                      <input
                        type="checkbox"
                        checked={assignIsClassTeacher}
                        onChange={(e) => setAssignIsClassTeacher(e.target.checked)}
                        disabled={isSubmitting || assignmentBlocked}
                        className="h-4 w-4 rounded border-border text-primary focus:ring-primary"
                      />
                      Mark this assignment as the class teacher for this class
                    </label>
                    {assignmentBlocked && (
                      <StatusBanner variant="warning">
                        {classes.length === 0
                          ? "Create a class before assigning this teacher."
                          : "Create a subject before assigning this teacher."}
                      </StatusBanner>
                    )}
                  </>
                )}
                {catalogError && (
                  <StatusBanner variant="warning">
                    {catalogError} You can still create the account and assign a class later.
                  </StatusBanner>
                )}
              </div>
            ) : (
              <StatusBanner variant="info">
                Admin accounts do not need class assignments. This is the path to add a second school admin.
              </StatusBanner>
            )}

            <StatusBanner variant="info">
              {role === "Admin"
                ? "Admins can later rotate this password with the existing password reset flow."
                : "Teachers can later rotate this password with the existing password reset flow."}
            </StatusBanner>

            {error && <StatusBanner variant="error">{error}</StatusBanner>}

            <div className="flex gap-2 pt-2">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? <Spinner size="sm" /> : `Create ${role}`}
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
          )}
        </CardContent>
      </PageSection>
    </PageShell>
  );
}
