"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { ArrowLeft, Search, UserCheck } from "lucide-react";
import type {
  ParentSearchResult,
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
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  const handleSearch = async (): Promise<void> => {
    if (phone.trim().length < 3) {
      setError("Enter at least 3 digits to search.");
      return;
    }

    setIsSearching(true);
    setError("");
    setSelectedParent(null);
    setSuccessMessage("");
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

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push(`/admin/students/${studentId}`)}
          aria-label="Back to student"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Link Parent</h1>
          <p className="text-muted-foreground">
            Search for a parent account by phone number and link them.
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

      <Card>
        <CardContent className="p-4 md:p-6">
          <div className="space-y-4 max-w-lg">
            <div className="flex gap-2">
              <div className="flex-1">
                <Input
                  label="Parent Phone Number"
                  placeholder="Enter phone number to search"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
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
                <Button
                  onClick={handleSearch}
                  disabled={isSearching || phone.trim().length < 3}
                  size="default"
                >
                  {isSearching ? (
                    <Spinner size="sm" />
                  ) : (
                    <>
                      <Search className="mr-1 h-4 w-4" />
                      Search
                    </>
                  )}
                </Button>
              </div>
            </div>

            {hasSearched && searchResults.length === 0 && (
              <div className="rounded-md border border-dashed p-4 text-center">
                <p className="text-sm text-muted-foreground">
                  No parent account found for this phone number. Make sure the
                  parent has an active account with the &quot;Parent&quot; role.
                </p>
              </div>
            )}

            {searchResults.length > 0 && (
              <div className="space-y-2">
                <p className="text-sm font-medium">
                  {searchResults.length} result
                  {searchResults.length !== 1 ? "s" : ""} found
                </p>
                <ul className="space-y-2" aria-label="Parent search results">
                  {searchResults.map((parent) => (
                    <li key={parent.id}>
                      <button
                        onClick={() => setSelectedParent(parent)}
                        className={`flex w-full items-center justify-between rounded-md border p-3 text-left transition-colors hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                          selectedParent?.id === parent.id
                            ? "border-primary bg-primary/5"
                            : ""
                        }`}
                        aria-pressed={selectedParent?.id === parent.id}
                      >
                        <div>
                          <p className="font-medium">{parent.name}</p>
                          <p className="text-sm text-muted-foreground">
                            {parent.phone}
                          </p>
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
              <div className="space-y-3 rounded-md border bg-muted/30 p-4">
                <p className="text-sm font-medium">
                  Linking: {selectedParent.name} ({selectedParent.phone})
                </p>
                <div className="space-y-2">
                  <label
                    htmlFor="relationship"
                    className="block text-sm font-medium"
                  >
                    Relationship
                  </label>
                  <select
                    id="relationship"
                    value={relationship}
                    onChange={(e) => setRelationship(e.target.value)}
                    disabled={isLinking}
                    className="flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
                  >
                    {RELATIONSHIP_OPTIONS.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                </div>
                <Button onClick={handleLink} disabled={isLinking}>
                  {isLinking ? <Spinner size="sm" /> : "Link Parent"}
                </Button>
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
