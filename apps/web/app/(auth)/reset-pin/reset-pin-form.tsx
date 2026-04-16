"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";

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
  const secureFieldClassName =
    "flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 text-sm text-foreground shadow-[0_12px_30px_-26px_rgba(15,40,69,0.42)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

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
        <StatusBanner variant="error">
          This reset link is missing a token. Please request a new one from the{" "}
          <Link href="/forgot-pin" className="underline">
            forgot PIN
          </Link>{" "}
          page.
        </StatusBanner>
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
            className={secureFieldClassName}
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
            className={secureFieldClassName}
          />
        </div>

        {error && (
          <StatusBanner id="form-error" variant="error">{error}</StatusBanner>
        )}
        {success && (
          <StatusBanner variant="success">{success}</StatusBanner>
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
