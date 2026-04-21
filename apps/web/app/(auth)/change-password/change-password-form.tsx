"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye, EyeOff } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { PASSWORD_POLICY_MESSAGE, validatePassword } from "@/lib/validation/password";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";

interface ChangePasswordResponse {
  message: string;
}

export function ChangePasswordForm(): React.ReactElement {
  const router = useRouter();
  const { user, isLoading: isAuthLoading, logout } = useAuth();

  const [currentPassword, setCurrentPassword] = React.useState<string>("");
  const [newPassword, setNewPassword] = React.useState<string>("");
  const [confirmPassword, setConfirmPassword] = React.useState<string>("");
  const [error, setError] = React.useState<string>("");
  const [success, setSuccess] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState<boolean>(false);
  const [showCurrent, setShowCurrent] = React.useState<boolean>(false);
  const [showNew, setShowNew] = React.useState<boolean>(false);

  const secureFieldClassName =
    "flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 text-sm text-foreground shadow-[0_12px_30px_-26px_rgba(15,40,69,0.42)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

  // If a parent somehow lands here, send them to change-pin instead.
  React.useEffect(() => {
    if (isAuthLoading) return;
    if (!user) {
      router.replace("/login");
      return;
    }
    if (user.role === "Parent") {
      router.replace("/change-pin");
    }
  }, [isAuthLoading, router, user]);

  const handleSubmit = async (
    e: React.FormEvent<HTMLFormElement>
  ): Promise<void> => {
    e.preventDefault();
    setError("");
    setSuccess("");

    if (!currentPassword) {
      setError("Please enter your current temporary password.");
      return;
    }
    const policy = validatePassword(newPassword);
    if (!policy.valid) {
      setError(policy.message);
      return;
    }
    if (newPassword === currentPassword) {
      setError("New password must be different from your current password.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New passwords do not match.");
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiPost<ChangePasswordResponse>(
        API_ENDPOINTS.changePassword,
        { currentPassword, newPassword, confirmPassword }
      );
      setSuccess(
        response.message ??
          "Password changed successfully. Please log in again."
      );
      // Backend revokes all refresh tokens, so we must log the user out locally
      // and bounce them to /login with their new password.
      setTimeout(() => {
        void logout().finally(() => {
          router.replace("/login");
        });
      }, 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Could not change password. Please try again.");
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
        <h2 className="text-lg font-semibold">Set a new password</h2>
        <p className="text-sm text-muted-foreground">
          For your security, please replace the temporary password you were
          issued before continuing.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2">
          <label
            htmlFor="currentPassword"
            className="block text-sm font-medium text-foreground"
          >
            Current (temporary) password
          </label>
          <div className="relative">
            <input
              id="currentPassword"
              type={showCurrent ? "text" : "password"}
              autoComplete="current-password"
              placeholder="••••••••"
              value={currentPassword}
              onChange={(e): void => {
                setCurrentPassword(e.target.value);
                setError("");
              }}
              disabled={isLoading}
              aria-invalid={!!error}
              className={secureFieldClassName}
            />
            <button
              type="button"
              onClick={(): void => setShowCurrent(!showCurrent)}
              disabled={isLoading}
              className="absolute right-4 top-1/2 -translate-y-1/2 inline-flex items-center text-sm font-medium text-muted-foreground hover:text-foreground disabled:opacity-50"
              aria-label={showCurrent ? "Hide password" : "Show password"}
            >
              {showCurrent ? (
                <EyeOff className="h-4 w-4" aria-hidden="true" />
              ) : (
                <Eye className="h-4 w-4" aria-hidden="true" />
              )}
            </button>
          </div>
        </div>

        <div className="space-y-2">
          <label
            htmlFor="newPassword"
            className="block text-sm font-medium text-foreground"
          >
            New password
          </label>
          <div className="relative">
            <input
              id="newPassword"
              type={showNew ? "text" : "password"}
              autoComplete="new-password"
              placeholder="••••••••"
              value={newPassword}
              onChange={(e): void => {
                setNewPassword(e.target.value);
                setError("");
              }}
              disabled={isLoading}
              aria-invalid={!!error}
              className={secureFieldClassName}
            />
            <button
              type="button"
              onClick={(): void => setShowNew(!showNew)}
              disabled={isLoading}
              className="absolute right-4 top-1/2 -translate-y-1/2 inline-flex items-center text-sm font-medium text-muted-foreground hover:text-foreground disabled:opacity-50"
              aria-label={showNew ? "Hide password" : "Show password"}
            >
              {showNew ? (
                <EyeOff className="h-4 w-4" aria-hidden="true" />
              ) : (
                <Eye className="h-4 w-4" aria-hidden="true" />
              )}
            </button>
          </div>
          <p className="text-xs text-muted-foreground">
            {PASSWORD_POLICY_MESSAGE} Choose something only you would know.
          </p>
        </div>

        <div className="space-y-2">
          <label
            htmlFor="confirmPassword"
            className="block text-sm font-medium text-foreground"
          >
            Confirm new password
          </label>
          <input
            id="confirmPassword"
            type={showNew ? "text" : "password"}
            autoComplete="new-password"
            placeholder="••••••••"
            value={confirmPassword}
            onChange={(e): void => {
              setConfirmPassword(e.target.value);
              setError("");
            }}
            disabled={isLoading}
            aria-invalid={!!error}
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
            "Save new password"
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
