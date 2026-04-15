"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  ApiError,
  apiGet,
  apiPost,
  type ProblemDetails,
} from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { PARENT_RELATIONSHIP_OPTIONS } from "@/lib/student-relationships";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { ClassSelector } from "@/components/shared/class-selector";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft } from "lucide-react";
import type {
  ClassItem,
  EnrollStudentRequest,
  MutationResponse,
} from "@/lib/types/student";

type FieldErrorKey =
  | "name"
  | "rollNumber"
  | "classId"
  | "parentName"
  | "phone"
  | "email"
  | "pin"
  | "relationship";

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const SERVER_FIELD_MAPPING: Record<string, FieldErrorKey> = {
  Name: "name",
  RollNumber: "rollNumber",
  ClassId: "classId",
  Phone: "phone",
  Email: "email",
  "Parent.Name": "parentName",
  "Parent.Phone": "phone",
  "Parent.Email": "email",
  "Parent.Pin": "pin",
  "Parent.Relationship": "relationship",
};

function getServerFieldErrors(
  details?: ProblemDetails
): Partial<Record<FieldErrorKey, string>> {
  if (!details?.errors) {
    return {};
  }

  return Object.entries(details.errors).reduce<Partial<Record<FieldErrorKey, string>>>(
    (errors, [key, messages]) => {
      const fieldKey = SERVER_FIELD_MAPPING[key];
      if (fieldKey && messages.length > 0) {
        errors[fieldKey] = messages[0];
      }
      return errors;
    },
    {}
  );
}

export default function EnrollStudentPage(): React.ReactElement {
  const router = useRouter();
  const [classes, setClasses] = React.useState<ClassItem[]>([]);

  const [name, setName] = React.useState("");
  const [rollNumber, setRollNumber] = React.useState("");
  const [classId, setClassId] = React.useState("");
  const [dateOfBirth, setDateOfBirth] = React.useState("");

  const [includeParentDetails, setIncludeParentDetails] = React.useState(false);
  const [parentName, setParentName] = React.useState("");
  const [parentPhone, setParentPhone] = React.useState("");
  const [parentEmail, setParentEmail] = React.useState("");
  const [parentPin, setParentPin] = React.useState("");
  const [relationship, setRelationship] = React.useState("parent");

  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<
    Partial<Record<FieldErrorKey, string>>
  >({});

  const clearParentFields = React.useCallback((): void => {
    setParentName("");
    setParentPhone("");
    setParentEmail("");
    setParentPin("");
    setRelationship("parent");
    setFieldErrors((current) => {
      const next = { ...current };
      delete next.parentName;
      delete next.phone;
      delete next.email;
      delete next.pin;
      delete next.relationship;
      return next;
    });
  }, []);

  React.useEffect(() => {
    const fetchClasses = async (): Promise<void> => {
      try {
        const data = await apiGet<ClassItem[]>(API_ENDPOINTS.classes);
        setClasses(data);
      } catch {
        setError("Failed to load classes.");
      }
    };
    fetchClasses();
  }, []);

  const validate = (): boolean => {
    const errors: Partial<Record<FieldErrorKey, string>> = {};

    if (!name.trim()) errors.name = "Student name is required.";
    if (name.trim().length > 120) {
      errors.name = "Name cannot exceed 120 characters.";
    }
    if (!rollNumber.trim()) errors.rollNumber = "Roll number is required.";
    if (!/^[A-Za-z0-9-]+$/.test(rollNumber.trim())) {
      errors.rollNumber =
        "Roll number can only contain letters, numbers, and hyphens.";
    }
    if (!classId) errors.classId = "Please select a class.";

    if (includeParentDetails) {
      if (!parentName.trim()) errors.parentName = "Parent name is required.";
      if (parentName.trim().length > 200) {
        errors.parentName = "Parent name cannot exceed 200 characters.";
      }
      if (!/^\d{10}$/.test(parentPhone.trim())) {
        errors.phone = "Phone number must be exactly 10 digits.";
      }
      if (!parentEmail.trim()) {
        errors.email = "Email is required.";
      } else if (!EMAIL_REGEX.test(parentEmail.trim())) {
        errors.email = "Enter a valid email address.";
      }
      if (!/^\d{4,6}$/.test(parentPin)) {
        errors.pin = "PIN must be 4-6 digits.";
      }
      if (!relationship) {
        errors.relationship = "Relationship is required.";
      }
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError("");

    if (!validate()) return;

    setIsSubmitting(true);
    try {
      const body: EnrollStudentRequest = {
        name: name.trim(),
        rollNumber: rollNumber.trim(),
        classId,
        dateOfBirth: dateOfBirth || null,
      };

      if (includeParentDetails) {
        body.parent = {
          name: parentName.trim(),
          phone: parentPhone.trim(),
          email: parentEmail.trim().toLowerCase(),
          pin: parentPin,
          relationship,
        };
      }

      const result = await apiPost<MutationResponse>(API_ENDPOINTS.students, body);
      router.push(`/admin/students/${result.studentId}`);
    } catch (err) {
      if (err instanceof ApiError) {
        const serverFieldErrors = getServerFieldErrors(err.details);
        if (Object.keys(serverFieldErrors).length > 0) {
          setFieldErrors(serverFieldErrors);
        }
        setError(err.message);
      } else {
        setError("Failed to enroll student.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Enroll Student"
        description="Create a new student profile and place them in the correct class."
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push("/admin/students")}
            aria-label="Back to students"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Students
          </Button>
        )}
        stats={[{ label: "Classes", value: classes.length.toString() }]}
      />

      <PageSection>
        <CardContent className="p-0">
          <form onSubmit={handleSubmit} className="max-w-2xl space-y-4">
            {classes.length === 0 && (
              <StatusBanner variant="warning">
                No classes are available yet. Create a class first, then return
                here to enroll the student.
              </StatusBanner>
            )}
            <Input
              label="Student Name"
              placeholder="Enter student's full name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.name}
            />

            <Input
              label="Roll Number"
              placeholder="e.g. 2026-5A-001"
              value={rollNumber}
              onChange={(e) => setRollNumber(e.target.value)}
              disabled={isSubmitting}
              error={fieldErrors.rollNumber}
            />

            <ClassSelector
              classes={classes}
              value={classId}
              onChange={setClassId}
              disabled={isSubmitting}
              error={fieldErrors.classId}
            />

            <Input
              label="Date of Birth (optional)"
              type="date"
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              disabled={isSubmitting}
            />

            <div className="space-y-4 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_20px_50px_-40px_rgba(15,23,42,0.42)] dark:bg-card/88">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="space-y-1">
                  <p className="text-sm font-medium text-foreground">
                    Parent Details
                  </p>
                  <p className="text-sm text-muted-foreground">
                    Optionally create and link one parent account during
                    enrollment instead of returning to the student profile later.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => {
                    setIncludeParentDetails((current) => {
                      if (current) {
                        clearParentFields();
                      }
                      return !current;
                    });
                  }}
                  disabled={isSubmitting}
                >
                  {includeParentDetails
                    ? "Remove Parent Details"
                    : "Add Parent Details"}
                </Button>
              </div>

              {includeParentDetails && (
                <div className="space-y-4">
                  <StatusBanner variant="info">
                    This creates the parent login immediately. You can still add
                    more parents later from the student profile.
                  </StatusBanner>

                  <Input
                    label="Parent Name"
                    placeholder="Enter parent name"
                    value={parentName}
                    onChange={(e) => setParentName(e.target.value)}
                    disabled={isSubmitting}
                    error={fieldErrors.parentName}
                  />

                  <Input
                    label="Phone Number"
                    placeholder="10-digit phone number"
                    value={parentPhone}
                    onChange={(e) =>
                      setParentPhone(
                        e.target.value.replace(/\D/g, "").slice(0, 10)
                      )
                    }
                    disabled={isSubmitting}
                    error={fieldErrors.phone}
                    inputMode="numeric"
                  />

                  <Input
                    label="Email"
                    type="email"
                    placeholder="parent@example.com"
                    value={parentEmail}
                    onChange={(e) => setParentEmail(e.target.value)}
                    disabled={isSubmitting}
                    error={fieldErrors.email}
                  />

                  <Input
                    label="Temporary PIN"
                    type="password"
                    placeholder="4-6 digit PIN"
                    value={parentPin}
                    onChange={(e) =>
                      setParentPin(
                        e.target.value.replace(/\D/g, "").slice(0, 6)
                      )
                    }
                    disabled={isSubmitting}
                    error={fieldErrors.pin}
                    inputMode="numeric"
                  />

                  <Select
                    id="relationship"
                    label="Relationship"
                    value={relationship}
                    onChange={(e) => setRelationship(e.target.value)}
                    disabled={isSubmitting}
                    error={fieldErrors.relationship}
                  >
                    {PARENT_RELATIONSHIP_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </Select>
                </div>
              )}
            </div>

            {error && <StatusBanner variant="error">{error}</StatusBanner>}

            <div className="flex gap-2 pt-2">
              <Button
                type="submit"
                disabled={isSubmitting || classes.length === 0}
              >
                {isSubmitting ? <Spinner size="sm" /> : "Enroll Student"}
              </Button>
              {classes.length === 0 && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => router.push("/admin/classes")}
                >
                  Manage Classes
                </Button>
              )}
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push("/admin/students")}
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
