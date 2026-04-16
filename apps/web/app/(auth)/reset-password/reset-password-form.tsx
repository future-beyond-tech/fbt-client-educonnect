"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";

interface ResetPasswordResponse {
  message: string;
}

export function ResetPasswordForm(): React.ReactElement {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [newPassword, setNewPassword] = React.useState<string>("");
  const [confirmPassword, setConfirmPassword] = React.useState<string>("");
  const [error, setError] = React.useState<string>("");
  const [success, setSuccess] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState<boolean>(false);
  const [showPassword, setShowPassword] = React.useState<boolean>(false);
  const secureFieldClassName =
    "flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 text-sm text-foreground shadow-[0_12px_30px_-26px_rgba(15,40,69,0.42)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

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
    if (newPassword.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiPost<ResetPasswordResponse>(
        API_ENDPOINTS.resetPassword,
        { token, newPassword, confirmPassword }
      );
      setSuccess(
        response.message ?? "Password reset successfully. Redirecting to login..."
      );
      setTimeout(() => router.replace("/login"), 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Could not reset password. Please try again.");
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
        <h2 className="text-lg font-semibold">Choose a new password</h2>
        <p className="text-sm text-muted-foreground">
          Enter and confirm your new password below.
        </p>
      </div>

      {!token && (
        <StatusBanner variant="error">
          This reset link is missing a token. Please request a new one from the{" "}
          <Link href="/forgot-password" className="underline">
            forgot password
          </Link>{" "}
          page.
        </StatusBanner>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
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
              type={showPassword ? "text" : "password"}
              autoComplete="new-password"
              placeholder="••••••••"
              value={newPassword}
              onChange={(e): void => {
                setNewPassword(e.target.value);
                setError("");
              }}
              disabled={isLoading || !token}
              aria-invalid={!!error}
              className={secureFieldClassName}
            />
            <button
              type="button"
              onClick={(): void => setShowPassword(!showPassword)}
              disabled={isLoading || !token}
              className="absolute right-4 top-1/2 -translate-y-1/2 text-sm font-medium text-muted-foreground hover:text-foreground disabled:opacity-50"
              aria-label={showPassword ? "Hide password" : "Show password"}
            >
              {showPassword ? "Hide" : "Show"}
            </button>
          </div>
          <p className="text-xs text-muted-foreground">
            At least 8 characters.
          </p>
        </div>

        <div className="space-y-2">
          <label
            htmlFor="confirmPassword"
            className="block text-sm font-medium text-foreground"
          >
            Confirm password
          </label>
          <input
            id="confirmPassword"
            type={showPassword ? "text" : "password"}
            autoComplete="new-password"
            placeholder="••••••••"
            value={confirmPassword}
            onChange={(e): void => {
              setConfirmPassword(e.target.value);
              setError("");
            }}
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
            "Reset password"
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
