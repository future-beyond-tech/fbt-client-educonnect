"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";

interface ResetPinResponse {
  message: string;
}

export function ResetPinForm(): React.ReactElement {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [newPin, setNewPin] = React.useState<string>("");
  const [confirmPin, setConfirmPin] = React.useState<string>("");
  const [error, setError] = React.useState<string>("");
  const [success, setSuccess] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState<boolean>(false);

  const handlePinChange = (
    setter: (value: string) => void,
    value: string
  ): void => {
    const cleaned = value.replace(/\D/g, "");
    if (cleaned.length <= 6) {
      setter(cleaned);
      setError("");
    }
  };

  const handleSubmit = async (
    e: React.FormEvent<HTMLFormElement>
  ): Promise<void> => {
    e.preventDefault();
    setError("");
    setSuccess("");

    if (!token) {
      setError("Reset link is missing or invalid.");
      return;
    }
    if (newPin.length < 4 || newPin.length > 6) {
      setError("PIN must be 4-6 digits.");
      return;
    }
    if (newPin !== confirmPin) {
      setError("PINs do not match.");
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiPost<ResetPinResponse>(
        API_ENDPOINTS.resetPin,
        { token, newPin, confirmPin }
      );
      setSuccess(
        response.message ?? "PIN reset successfully. Redirecting to login..."
      );
      setTimeout(() => router.replace("/login"), 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Could not reset PIN. Please try again.");
      } else {
        setError("An unexpected error occurred. Please try again.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold">Choose a new PIN</h2>
        <p className="text-sm text-muted-foreground">
          Enter and confirm a new 4-6 digit PIN.
        </p>
      </div>

      {!token && (
        <p className="text-sm font-medium text-destructive">
          This reset link is missing a token. Please request a new one from the{" "}
          <Link href="/forgot-pin" className="underline">
            forgot PIN
          </Link>{" "}
          page.
        </p>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2">
          <label
            htmlFor="newPin"
            className="block text-sm font-medium text-foreground"
          >
            New PIN
          </label>
          <input
            id="newPin"
            type="password"
            inputMode="numeric"
            pattern="[0-9]*"
            maxLength={6}
            placeholder="••••"
            value={newPin}
            onChange={(e): void => handlePinChange(setNewPin, e.target.value)}
            disabled={isLoading || !token}
            aria-invalid={!!error}
            className="flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
          />
        </div>

        <div className="space-y-2">
          <label
            htmlFor="confirmPin"
            className="block text-sm font-medium text-foreground"
          >
            Confirm PIN
          </label>
          <input
            id="confirmPin"
            type="password"
            inputMode="numeric"
            pattern="[0-9]*"
            maxLength={6}
            placeholder="••••"
            value={confirmPin}
            onChange={(e): void =>
              handlePinChange(setConfirmPin, e.target.value)
            }
            disabled={isLoading || !token}
            aria-invalid={!!error}
            className="flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
          />
        </div>

        {error && (
          <p id="form-error" className="text-sm font-medium text-destructive">
            {error}
          </p>
        )}
        {success && (
          <p className="text-sm font-medium text-green-700">{success}</p>
        )}

        <Button
          type="submit"
          className="w-full h-11 min-h-11"
          disabled={isLoading || !token}
        >
          {isLoading ? (
            <>
              <Spinner size="sm" />
              <span>Resetting...</span>
            </>
          ) : (
            "Reset PIN"
          )}
        </Button>
      </form>

      <div className="text-center text-sm">
        <Link href="/login" className="text-primary hover:underline">
          Back to login
        </Link>
      </div>
    </div>
  );
}
