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
import {
  isValidJapanPhone,
  JAPAN_PHONE_LOCAL_DIGITS,
  JAPAN_PHONE_VALIDATION_MESSAGE,
  normalizeJapanPhoneInput,
  normalizeJapanPhoneSearch,
} from "@/lib/phone";
import { PARENT_RELATIONSHIP_OPTIONS } from "@/lib/student-relationships";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { ClassSelector } from "@/components/shared/class-selector";
import {
  PageHeader,
  PageSection,
  PageShell,
} from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft, Search, UserCheck } from "lucide-react";
import type {
  ClassItem,
  EnrollStudentRequest,
  MutationResponse,
  ParentSearchResult,
} from "@/lib/types/student";

type ParentMode = "none" | "existing" | "new";

type FieldErrorKey =
  | "name"
  | "rollNumber"
  | "classId"
  | "parentMode"
  | "parentName"
  | "phone"
  | "email"
  | "pin"
  | "relationship"
  | "existingParent"
  | "existingRelationship";

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const SERVER_FIELD_MAPPING: Record<string, FieldErrorKey> = {
  Name: "name",
  RollNumber: "rollNumber",
  ClassId: "classId",
  Phone: "phone",
  Email: "email",
  Parent: "parentMode",
  ExistingParent: "parentMode",
  "Parent.Name": "parentName",
  "Parent.Phone": "phone",
  "Parent.Email": "email",
  "Parent.Pin": "pin",
  "Parent.Relationship": "relationship",
  "ExistingParent.ParentId": "existingParent",
  "ExistingParent.Relationship": "existingRelationship",
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

  const [parentMode, setParentMode] = React.useState<ParentMode>("none");
  const [parentName, setParentName] = React.useState("");
  const [parentPhone, setParentPhone] = React.useState("");
  const [parentEmail, setParentEmail] = React.useState("");
  const [parentPin, setParentPin] = React.useState("");
  const [relationship, setRelationship] = React.useState("parent");

  const [existingParentPhone, setExistingParentPhone] = React.useState("");
  const [searchResults, setSearchResults] = React.useState<
    ParentSearchResult[]
  >([]);
  const [hasSearchedExistingParent, setHasSearchedExistingParent] =
    React.useState(false);
  const [isSearchingExistingParent, setIsSearchingExistingParent] =
    React.useState(false);
  const [selectedExistingParent, setSelectedExistingParent] =
    React.useState<ParentSearchResult | null>(null);
  const [existingRelationship, setExistingRelationship] =
    React.useState("parent");

  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [error, setError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<
    Partial<Record<FieldErrorKey, string>>
  >({});

  const clearNewParentFields = React.useCallback((): void => {
    setParentName("");
    setParentPhone("");
    setParentEmail("");
    setParentPin("");
    setRelationship("parent");
    setFieldErrors((current) => {
      const next = { ...current };
      delete next.parentMode;
      delete next.parentName;
      delete next.phone;
      delete next.email;
      delete next.pin;
      delete next.relationship;
      return next;
    });
  }, []);

  const clearExistingParentFields = React.useCallback((): void => {
    setExistingParentPhone("");
    setSearchResults([]);
    setHasSearchedExistingParent(false);
    setSelectedExistingParent(null);
    setExistingRelationship("parent");
    setFieldErrors((current) => {
      const next = { ...current };
      delete next.parentMode;
      delete next.existingParent;
      delete next.existingRelationship;
      return next;
    });
  }, []);

  const handleParentModeChange = React.useCallback(
    (nextMode: ParentMode): void => {
      setParentMode(nextMode);
      setError("");

      if (nextMode !== "new") {
        clearNewParentFields();
      }

      if (nextMode !== "existing") {
        clearExistingParentFields();
      }
    },
    [clearExistingParentFields, clearNewParentFields]
  );

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

  const handleSearchExistingParent = async (): Promise<void> => {
    const searchPhone = normalizeJapanPhoneSearch(existingParentPhone.trim());

    if (searchPhone.length < 3) {
      setError("Enter at least 3 digits to search.");
      return;
    }

    setIsSearchingExistingParent(true);
    setError("");
    setSearchResults([]);
    setHasSearchedExistingParent(false);
    setSelectedExistingParent(null);
    setFieldErrors((current) => {
      const next = { ...current };
      delete next.existingParent;
      delete next.existingRelationship;
      return next;
    });

    try {
      const data = await apiGet<ParentSearchResult[]>(
        `${API_ENDPOINTS.studentsSearchParents}?phone=${encodeURIComponent(searchPhone)}`
      );
      setSearchResults(data);
      setHasSearchedExistingParent(true);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to search parents."
      );
    } finally {
      setIsSearchingExistingParent(false);
    }
  };

  const validate = (): boolean => {
    const errors: Partial<Record<FieldErrorKey, string>> = {};

    if (!name.trim()) {
      errors.name = "Student name is required.";
    } else if (name.trim().length > 120) {
      errors.name = "Name cannot exceed 120 characters.";
    }

    if (!rollNumber.trim()) {
      errors.rollNumber = "Roll number is required.";
    } else if (!/^[A-Za-z0-9-]+$/.test(rollNumber.trim())) {
      errors.rollNumber =
        "Roll number can only contain letters, numbers, and hyphens.";
    }

    if (!classId) {
      errors.classId = "Please select a class.";
    }

    if (parentMode === "new") {
      if (!parentName.trim()) {
        errors.parentName = "Parent name is required.";
      } else if (parentName.trim().length > 200) {
        errors.parentName = "Parent name cannot exceed 200 characters.";
      }

      if (!isValidJapanPhone(parentPhone.trim())) {
        errors.phone = JAPAN_PHONE_VALIDATION_MESSAGE;
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

    if (parentMode === "existing") {
      if (!selectedExistingParent) {
        errors.existingParent =
          "Search and select an existing parent account to link.";
      }

      if (!existingRelationship) {
        errors.existingRelationship = "Relationship is required.";
      }
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError("");

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);
    try {
      const body: EnrollStudentRequest = {
        name: name.trim(),
        rollNumber: rollNumber.trim(),
        classId,
        dateOfBirth: dateOfBirth || null,
      };

      if (parentMode === "new") {
        body.parent = {
          name: parentName.trim(),
          phone: parentPhone.trim(),
          email: parentEmail.trim().toLowerCase(),
          pin: parentPin,
          relationship,
        };
      }

      if (parentMode === "existing" && selectedExistingParent) {
        body.existingParent = {
          parentId: selectedExistingParent.id,
          relationship: existingRelationship,
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
              <div className="space-y-1">
                <p className="text-sm font-medium text-foreground">
                  Parent Details
                </p>
                <p className="text-sm text-muted-foreground">
                  Choose whether to skip parent setup for now, reuse an existing
                  parent login, or create a brand new parent account during
                  enrollment.
                </p>
              </div>

              <Select
                id="parent-mode"
                label="Parent Setup"
                value={parentMode}
                onChange={(e) =>
                  handleParentModeChange(e.target.value as ParentMode)
                }
                disabled={isSubmitting}
                error={fieldErrors.parentMode}
                hint="You can always add or link more parents later from the student profile."
              >
                <option value="none">No parent for now</option>
                <option value="existing">Link existing parent</option>
                <option value="new">Create new parent</option>
              </Select>

              {parentMode === "none" && (
                <StatusBanner variant="info">
                  The student will be created without a parent link. You can
                  connect parents later from the student profile whenever you
                  need to.
                </StatusBanner>
              )}

              {parentMode === "existing" && (
                <div className="space-y-4">
                  <StatusBanner variant="info">
                    Reuse an existing parent account so one login can see
                    multiple children.
                  </StatusBanner>

                  <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                    <div className="flex-1">
                      <Input
                        label="Parent Phone Number"
                        placeholder="Search by phone number"
                        value={existingParentPhone}
                        onChange={(e) =>
                          setExistingParentPhone(
                            normalizeJapanPhoneInput(e.target.value)
                          )
                        }
                        disabled={isSubmitting || isSearchingExistingParent}
                        inputMode="numeric"
                        maxLength={JAPAN_PHONE_LOCAL_DIGITS}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") {
                            e.preventDefault();
                            void handleSearchExistingParent();
                          }
                        }}
                      />
                    </div>
                    <Button
                      type="button"
                      onClick={() => void handleSearchExistingParent()}
                      disabled={
                        isSubmitting ||
                        isSearchingExistingParent ||
                        normalizeJapanPhoneSearch(existingParentPhone.trim())
                          .length < 3
                      }
                    >
                      {isSearchingExistingParent ? (
                        <Spinner size="sm" />
                      ) : (
                        <>
                          <Search className="h-4 w-4" />
                          Search Parent
                        </>
                      )}
                    </Button>
                  </div>

                  {hasSearchedExistingParent && searchResults.length === 0 && (
                    <div className="rounded-[22px] border border-dashed border-border/80 bg-card/50 p-5 text-center">
                      <p className="text-sm text-muted-foreground">
                        No active parent account matched that phone number. Try
                        another search or switch to creating a new parent.
                      </p>
                    </div>
                  )}

                  {searchResults.length > 0 && (
                    <div className="space-y-3">
                      <p className="text-sm font-medium">
                        {searchResults.length} result
                        {searchResults.length !== 1 ? "s" : ""} found
                      </p>
                      <ul
                        className="space-y-3"
                        aria-label="Existing parent search results"
                      >
                        {searchResults.map((parent) => (
                          <li key={parent.id}>
                            <button
                              type="button"
                              onClick={() => {
                                setSelectedExistingParent(parent);
                                setFieldErrors((current) => {
                                  const next = { ...current };
                                  delete next.existingParent;
                                  return next;
                                });
                              }}
                              className={`flex w-full items-center justify-between rounded-[24px] border p-4 text-left transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                                selectedExistingParent?.id === parent.id
                                  ? "border-primary bg-primary/5"
                                  : "border-border/70 bg-card/80 dark:bg-card/90"
                              }`}
                              aria-pressed={selectedExistingParent?.id === parent.id}
                            >
                              <div>
                                <p className="font-medium">{parent.name}</p>
                                <p className="text-sm text-muted-foreground">
                                  {parent.phone}
                                </p>
                                {parent.email && (
                                  <p className="text-xs text-muted-foreground">
                                    {parent.email}
                                  </p>
                                )}
                              </div>
                              {selectedExistingParent?.id === parent.id && (
                                <UserCheck
                                  className="h-5 w-5 text-primary"
                                  aria-hidden="true"
                                />
                              )}
                            </button>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {fieldErrors.existingParent && (
                    <p className="text-sm font-medium text-destructive">
                      {fieldErrors.existingParent}
                    </p>
                  )}

                  <Select
                    id="existing-relationship"
                    label="Relationship"
                    value={existingRelationship}
                    onChange={(e) => setExistingRelationship(e.target.value)}
                    disabled={isSubmitting}
                    error={fieldErrors.existingRelationship}
                  >
                    {PARENT_RELATIONSHIP_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </Select>
                </div>
              )}

              {parentMode === "new" && (
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
                    placeholder="11-digit phone number"
                    value={parentPhone}
                    onChange={(e) =>
                      setParentPhone(normalizeJapanPhoneInput(e.target.value))
                    }
                    disabled={isSubmitting}
                    error={fieldErrors.phone}
                    inputMode="numeric"
                    maxLength={JAPAN_PHONE_LOCAL_DIGITS}
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
