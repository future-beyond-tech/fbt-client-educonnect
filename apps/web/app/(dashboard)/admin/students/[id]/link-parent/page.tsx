"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CardContent } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
import { ArrowLeft, Search, UserCheck } from "lucide-react";
import type {
  ParentSearchResult,
  CreateParentRequest,
  ParentMutationResponse,
  LinkParentRequest,
  MutationResponse,
} from "@/lib/types/student";

const RELATIONSHIP_OPTIONS = [
  { value: "parent", label: "Parent" },
  { value: "guardian", label: "Guardian" },
  { value: "grandparent", label: "Grandparent" },
  { value: "sibling", label: "Sibling" },
  { value: "other", label: "Other" },
];

export default function LinkParentPage(): React.ReactElement {
  const params = useParams();
  const router = useRouter();
  const studentId = params.id as string;

  const [phone, setPhone] = React.useState("");
  const [searchResults, setSearchResults] = React.useState<
    ParentSearchResult[]
  >([]);
  const [hasSearched, setHasSearched] = React.useState(false);
  const [isSearching, setIsSearching] = React.useState(false);

  const [selectedParent, setSelectedParent] =
    React.useState<ParentSearchResult | null>(null);
  const [relationship, setRelationship] = React.useState("parent");
  const [isLinking, setIsLinking] = React.useState(false);
  const [showCreateForm, setShowCreateForm] = React.useState(false);
  const [newParentName, setNewParentName] = React.useState("");
  const [newParentPhone, setNewParentPhone] = React.useState("");
  const [newParentEmail, setNewParentEmail] = React.useState("");
  const [newParentPin, setNewParentPin] = React.useState("");
  const [newRelationship, setNewRelationship] = React.useState("parent");
  const [isCreatingParent, setIsCreatingParent] = React.useState(false);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  const resetCreateForm = React.useCallback((): void => {
    setShowCreateForm(false);
    setNewParentName("");
    setNewParentPhone("");
    setNewParentEmail("");
    setNewParentPin("");
    setNewRelationship("parent");
  }, []);

  const handleSearch = async (): Promise<void> => {
    if (phone.trim().length < 3) {
      setError("Enter at least 3 digits to search.");
      return;
    }

    setIsSearching(true);
    setError("");
    setSelectedParent(null);
    setSuccessMessage("");
    resetCreateForm();
    try {
      const data = await apiGet<ParentSearchResult[]>(
        `${API_ENDPOINTS.studentsSearchParents}?phone=${encodeURIComponent(phone.trim())}`
      );
      setSearchResults(data);
      setHasSearched(true);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to search parents."
      );
    } finally {
      setIsSearching(false);
    }
  };

  const handleLink = async (): Promise<void> => {
    if (!selectedParent) return;

    setIsLinking(true);
    setError("");
    try {
      const body: LinkParentRequest = {
        parentId: selectedParent.id,
        relationship,
      };
      const result = await apiPost<MutationResponse>(
        `${API_ENDPOINTS.students}/${studentId}/parent-links`,
        body
      );
      setSuccessMessage(result.message);
      setSelectedParent(null);
      setSearchResults([]);
      setPhone("");
      setHasSearched(false);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to link parent.");
      }
    } finally {
      setIsLinking(false);
    }
  };

  const openCreateForm = (): void => {
    setSelectedParent(null);
    setError("");
    setSuccessMessage("");
    setShowCreateForm(true);
    setNewParentPhone(phone.trim());
  };

  const handleCreateParentAndLink = async (): Promise<void> => {
    const trimmedName = newParentName.trim();
    const trimmedPhone = newParentPhone.trim();
    const trimmedEmail = newParentEmail.trim().toLowerCase();

    if (!trimmedName || !trimmedPhone || !trimmedEmail || !newParentPin) {
      setError("Name, phone, email, and PIN are required.");
      return;
    }

    if (!/^\d{10}$/.test(trimmedPhone)) {
      setError("Phone number must be exactly 10 digits.");
      return;
    }

    if (!/^\d{4,6}$/.test(newParentPin)) {
      setError("PIN must be 4-6 digits.");
      return;
    }

    setIsCreatingParent(true);
    setError("");
    setSuccessMessage("");

    let createdParentId: string | undefined;

    try {
      const parentBody: CreateParentRequest = {
        name: trimmedName,
        phone: trimmedPhone,
        email: trimmedEmail,
        pin: newParentPin,
      };

      const parentResult = await apiPost<ParentMutationResponse>(
        API_ENDPOINTS.parents,
        parentBody
      );
      createdParentId = parentResult.parentId;

      if (!createdParentId) {
        throw new Error("Parent creation did not return an ID.");
      }

      const linkBody: LinkParentRequest = {
        parentId: createdParentId,
        relationship: newRelationship,
      };

      const linkResult = await apiPost<MutationResponse>(
        `${API_ENDPOINTS.students}/${studentId}/parent-links`,
        linkBody
      );

      setSuccessMessage(`Parent created and linked successfully. ${linkResult.message}`);
      setSearchResults([]);
      setHasSearched(false);
      setPhone("");
      resetCreateForm();
    } catch (err) {
      if (createdParentId) {
        setError(
          "Parent account was created, but linking failed. Search by phone and link the parent manually."
        );
      } else if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to create parent.");
      }
    } finally {
      setIsCreatingParent(false);
    }
  };

  return (
    <PageShell>
      <PageHeader
        eyebrow="Admin operations"
        title="Link Parent"
        description="Search by phone number, choose the correct parent account, and connect it to the student."
        backAction={(
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push(`/admin/students/${studentId}`)}
            aria-label="Back to student"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Student
          </Button>
        )}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}
      {error && (
        <StatusBanner variant="error">{error}</StatusBanner>
      )}

      <PageSection>
        <CardContent className="p-0">
          <div className="max-w-2xl space-y-4">
            <div className="flex gap-2">
              <div className="flex-1">
                <Input
                  label="Parent Phone Number"
                  placeholder="Enter phone number to search"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 10))}
                  disabled={isSearching}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      handleSearch();
                    }
                  }}
                />
              </div>
              <div className="flex items-end">
                <div className="flex gap-2">
                  <Button
                    onClick={handleSearch}
                    disabled={isSearching || phone.trim().length < 3}
                    size="default"
                  >
                    {isSearching ? (
                      <Spinner size="sm" />
                    ) : (
                      <>
                        <Search className="h-4 w-4" />
                        Search
                      </>
                    )}
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={openCreateForm}
                    disabled={isSearching}
                  >
                    Create New Parent
                  </Button>
                </div>
              </div>
            </div>

            {hasSearched && searchResults.length === 0 && (
              <div className="rounded-[22px] border border-dashed border-border/80 bg-card/50 p-5 text-center">
                <p className="text-sm text-muted-foreground">
                  No parent account found for this phone number. Make sure the
                  parent has an active account with the &quot;Parent&quot; role.
                </p>
              </div>
            )}

            {searchResults.length > 0 && (
              <div className="space-y-3">
                <p className="text-sm font-medium">
                  {searchResults.length} result
                  {searchResults.length !== 1 ? "s" : ""} found
                </p>
                <ul className="space-y-3" aria-label="Parent search results">
                  {searchResults.map((parent) => (
                    <li key={parent.id}>
                      <button
                        onClick={() => setSelectedParent(parent)}
                          className={`flex w-full items-center justify-between rounded-[24px] border p-4 text-left transition-all hover:-translate-y-0.5 hover:border-primary/20 hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                          selectedParent?.id === parent.id
                            ? "border-primary bg-primary/5"
                            : "border-border/70 bg-card/80 dark:bg-card/90"
                        }`}
                        aria-pressed={selectedParent?.id === parent.id}
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
                        {selectedParent?.id === parent.id && (
                          <UserCheck className="h-5 w-5 text-primary" />
                        )}
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {selectedParent && (
              <div className="space-y-3 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_20px_50px_-40px_rgba(15,23,42,0.42)] dark:bg-card/88">
                <p className="text-sm font-medium">
                  Linking: {selectedParent.name} ({selectedParent.phone})
                </p>
                <Select
                  id="relationship"
                  label="Relationship"
                  value={relationship}
                  onChange={(e) => setRelationship(e.target.value)}
                  disabled={isLinking}
                >
                  {RELATIONSHIP_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </Select>
                <Button onClick={handleLink} disabled={isLinking}>
                  {isLinking ? <Spinner size="sm" /> : "Link Parent"}
                </Button>
              </div>
            )}

            {showCreateForm && (
              <div className="space-y-3 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_20px_50px_-40px_rgba(15,23,42,0.42)] dark:bg-card/88">
                <p className="text-sm font-medium">Create and link a new parent account</p>
                <Input
                  label="Parent Name"
                  value={newParentName}
                  onChange={(e) => setNewParentName(e.target.value)}
                  disabled={isCreatingParent}
                  placeholder="Enter parent name"
                />
                <Input
                  label="Phone Number"
                  value={newParentPhone}
                  onChange={(e) => setNewParentPhone(e.target.value.replace(/\D/g, "").slice(0, 10))}
                  disabled={isCreatingParent}
                  placeholder="10-digit phone number"
                  inputMode="numeric"
                />
                <Input
                  label="Email"
                  type="email"
                  value={newParentEmail}
                  onChange={(e) => setNewParentEmail(e.target.value)}
                  disabled={isCreatingParent}
                  placeholder="parent@example.com"
                />
                <Input
                  label="Temporary PIN"
                  type="password"
                  value={newParentPin}
                  onChange={(e) => setNewParentPin(e.target.value.replace(/\D/g, "").slice(0, 6))}
                  disabled={isCreatingParent}
                  placeholder="4-6 digit PIN"
                  inputMode="numeric"
                />
                <Select
                  id="newRelationship"
                  label="Relationship"
                  value={newRelationship}
                  onChange={(e) => setNewRelationship(e.target.value)}
                  disabled={isCreatingParent}
                >
                  {RELATIONSHIP_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </Select>
                <StatusBanner variant="info">
                  The parent can later use this email to reset their PIN.
                </StatusBanner>
                <div className="flex gap-2">
                  <Button onClick={handleCreateParentAndLink} disabled={isCreatingParent}>
                    {isCreatingParent ? <Spinner size="sm" /> : "Create and Link Parent"}
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={resetCreateForm}
                    disabled={isCreatingParent}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </div>
        </CardContent>
      </PageSection>
    </PageShell>
  );
}
