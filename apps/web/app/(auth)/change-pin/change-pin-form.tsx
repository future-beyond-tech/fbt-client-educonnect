"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";

interface ChangePinResponse {
  message: string;
}

const PIN_REGEX = /^[0-9]{4,6}$/;

export function ChangePinForm(): React.ReactElement {
  const router = useRouter();
  const { user, isLoading: isAuthLoading, logout } = useAuth();

  const [currentPin, setCurrentPin] = React.useState<string>("");
  const [newPin, setNewPin] = React.useState<string>("");
  const [confirmPin, setConfirmPin] = React.useState<string>("");
  const [error, setError] = React.useState<string>("");
  const [success, setSuccess] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState<boolean>(false);

  const secureFieldClassName =
    "flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 text-sm tracking-[0.4em] text-foreground shadow-[0_12px_30px_-26px_rgba(15,40,69,0.42)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

  // If a staff member somehow lands here, send them to change-password.
  React.useEffect(() => {
    if (isAuthLoading) return;
    if (!user) {
      router.replace("/login");
      return;
    }
    if (user.role !== "Parent") {
      router.replace("/change-password");
    }
  }, [isAuthLoading, router, user]);

  const handlePinChange = (
    setter: React.Dispatch<React.SetStateAction<string>>
  ) => (value: string): void => {
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

    if (!PIN_REGEX.test(currentPin)) {
      setError("Please enter your current PIN (4–6 digits).");
      return;
    }
    if (!PIN_REGEX.test(newPin)) {
      setError("New PIN must be 4–6 digits.");
      return;
    }
    if (newPin === currentPin) {
      setError("New PIN must be different from your current PIN.");
      return;
    }
    if (newPin !== confirmPin) {
      setError("New PINs do not match.");
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiPost<ChangePinResponse>(
        API_ENDPOINTS.changePin,
        { currentPin, newPin, confirmPin }
      );
      setSuccess(
        response.message ?? "PIN changed successfully. Please log in again."
      );
      setTimeout(() => {
        void logout().finally(() => {
          router.replace("/login");
        });
      }, 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Could not change PIN. Please try again.");
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
        <h2 className="text-lg font-semibold">Set a new parent PIN</h2>
        <p className="text-sm text-muted-foreground">
          Please replace the temporary PIN you received from the school before
          continuing.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2">
          <label
            htmlFor="currentPin"
            className="block text-sm font-medium text-foreground"
          >
            Current (temporary) PIN
          </label>
          <input
            id="currentPin"
            type="password"
            inputMode="numeric"
            pattern="[0-9]*"
            autoComplete="current-password"
            placeholder="••••"
            value={currentPin}
            onChange={(e): void => handlePinChange(setCurrentPin)(e.target.value)}
            disabled={isLoading}
            aria-invalid={!!error}
            maxLength={6}
            className={secureFieldClassName}
          />
        </div>

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
            autoComplete="new-password"
            placeholder="••••"
            value={newPin}
            onChange={(e): void => handlePinChange(setNewPin)(e.target.value)}
            disabled={isLoading}
            aria-invalid={!!error}
            maxLength={6}
            className={secureFieldClassName}
          />
          <p className="text-xs text-muted-foreground">
            4–6 digits. Choose a PIN only you will remember.
          </p>
        </div>

        <div className="space-y-2">
          <label
            htmlFor="confirmPin"
            className="block text-sm font-medium text-foreground"
          >
            Confirm new PIN
          </label>
          <input
            id="confirmPin"
            type="password"
            inputMode="numeric"
            pattern="[0-9]*"
            autoComplete="new-password"
            placeholder="••••"
            value={confirmPin}
            onChange={(e): void => handlePinChange(setConfirmPin)(e.target.value)}
            disabled={isLoading}
            aria-invalid={!!error}
            maxLength={6}
            className={secureFieldClassName}
          />
        </div>

        {error && (
          <StatusBanner id="form-error" variant="error">
            {error}
          </StatusBanner>
        )}
        {success && <StatusBanner variant="success">{success}</StatusBanner>}

        <Button
          type="submit"
          className="w-full h-11 min-h-11"
          disabled={isLoading}
        >
          {isLoading ? (
            <>
              <Spinner size="sm" />
              <span>Saving...</span>
            </>
          ) : (
            "Save new PIN"
          )}
        </Button>
      </form>

      <div className="text-center text-sm">
        <Link
          href="/login"
          onClick={(e): void => {
            e.preventDefault();
            void logout().finally(() => router.replace("/login"));
          }}
          className="text-primary hover:underline"
        >
          Cancel and sign out
        </Link>
      </div>
    </div>
  );
}
