"use client";

import * as React from "react";
import Link from "next/link";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";

interface ForgotPinResponse {
  message: string;
}

export function ForgotPinForm(): React.ReactElement {
  const [email, setEmail] = React.useState<string>("");
  const [error, setError] = React.useState<string>("");
  const [success, setSuccess] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState<boolean>(false);

  const handleSubmit = async (
    e: React.FormEvent<HTMLFormElement>
  ): Promise<void> => {
    e.preventDefault();
    setError("");
    setSuccess("");

    if (!email.trim()) {
      setError("Please enter your email address.");
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiPost<ForgotPinResponse>(
        API_ENDPOINTS.forgotPin,
        { email: email.trim() }
      );
      setSuccess(
        response.message ??
          "If a parent account exists for that email, a reset link has been sent."
      );
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Could not send reset link. Please try again.");
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
        <h2 className="text-lg font-semibold">Forgot your PIN?</h2>
        <p className="text-sm text-muted-foreground">
          Enter the email associated with your parent account and we&apos;ll
          send you a link to choose a new PIN.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          placeholder="you@example.com"
          value={email}
          onChange={(e): void => {
            setEmail(e.target.value);
            setError("");
          }}
          disabled={isLoading}
          aria-invalid={!!error}
          aria-describedby={error ? "form-error" : undefined}
        />

        {error && (
          <StatusBanner id="form-error" variant="error">{error}</StatusBanner>
        )}
        {success && (
          <StatusBanner variant="success">{success}</StatusBanner>
        )}

        <Button
          type="submit"
          className="w-full h-11 min-h-11"
          disabled={isLoading}
        >
          {isLoading ? (
            <>
              <Spinner size="sm" />
              <span>Sending link...</span>
            </>
          ) : (
            "Send reset link"
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
