"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS, defaultRouteByRole } from "@/lib/constants";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

interface LoginResponse {
  accessToken: string;
}

type LoginMode = "parent" | "staff";

export function LoginForm(): React.ReactElement {
  const router = useRouter();
  const { login, user, isLoading: isAuthLoading } = useAuth();
  const [mode, setMode] = React.useState<LoginMode>("parent");
  const [phone, setPhone] = React.useState<string>("");
  const [rollNumber, setRollNumber] = React.useState<string>("");
  const [pin, setPin] = React.useState<string>("");
  const [password, setPassword] = React.useState<string>("");
  const [error, setError] = React.useState<string>("");
  const [isLoading, setIsLoading] = React.useState<boolean>(false);
  const [showPassword, setShowPassword] = React.useState<boolean>(false);

  React.useEffect(() => {
    if (user) {
      router.replace(defaultRouteByRole[user.role]);
    }
  }, [router, user]);

  const validatePhoneNumber = (phoneNumber: string): boolean => {
    const cleaned = phoneNumber.replace(/\D/g, "");
    return cleaned.length === 10;
  };

  const handlePhoneChange = (value: string): void => {
    const cleaned = value.replace(/\D/g, "");
    if (cleaned.length <= 10) {
      setPhone(cleaned);
      setError("");
    }
  };

  const handlePinChange = (value: string): void => {
    const cleaned = value.replace(/\D/g, "");
    if (cleaned.length <= 6) {
      setPin(cleaned);
      setError("");
    }
  };

  const handlePasswordChange = (value: string): void => {
    setPassword(value);
    setError("");
  };

  const handleRollNumberChange = (value: string): void => {
    setRollNumber(value.trim());
    setError("");
  };

  const handleModeSwitch = (newMode: LoginMode): void => {
    setMode(newMode);
    setPhone("");
    setRollNumber("");
    setPin("");
    setPassword("");
    setError("");
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>): Promise<void> => {
    e.preventDefault();
    setError("");

    if (mode === "parent") {
      if (!rollNumber) {
        setError("Please enter your child's roll number");
        return;
      }
      if (pin.length < 4 || pin.length > 6) {
        setError("PIN must be 4-6 digits");
        return;
      }
    } else {
      if (!validatePhoneNumber(phone)) {
        setError("Please enter a valid 10-digit phone number");
        return;
      }
      if (!password) {
        setError("Password is required");
        return;
      }
    }

    setIsLoading(true);

    try {
      const endpoint =
        mode === "parent" ? API_ENDPOINTS.loginParent : API_ENDPOINTS.login;
      const payload =
        mode === "parent"
          ? { rollNumber, pin }
          : { phone, password };

      const response = await apiPost<LoginResponse>(endpoint, payload);
      login(response.accessToken);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Login failed. Please try again.");
      } else {
        setError("An unexpected error occurred. Please try again.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      {isAuthLoading && (
        <p className="text-sm text-muted-foreground">
          Restoring your previous session...
        </p>
      )}
      <div className="flex gap-2">
        <Button
          type="button"
          variant={mode === "parent" ? "default" : "outline"}
          className="flex-1 h-11 min-h-11"
          onClick={() => handleModeSwitch("parent")}
          disabled={isLoading || isAuthLoading}
          aria-pressed={mode === "parent"}
        >
          I&apos;m a Parent
        </Button>
        <Button
          type="button"
          variant={mode === "staff" ? "default" : "outline"}
          className="flex-1 h-11 min-h-11"
          onClick={() => handleModeSwitch("staff")}
          disabled={isLoading || isAuthLoading}
          aria-pressed={mode === "staff"}
        >
          I&apos;m Staff
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        {mode === "parent" ? (
          <div className="space-y-2">
            <label
              htmlFor="rollNumber"
              className="block text-sm font-medium text-foreground"
            >
              Child&apos;s Roll Number
            </label>
            <Input
              id="rollNumber"
              type="text"
              placeholder="e.g. 2024001"
              value={rollNumber}
              onChange={(e): void => handleRollNumberChange(e.target.value)}
              disabled={isLoading || isAuthLoading}
              aria-invalid={!!error}
              aria-describedby={error ? "form-error" : undefined}
            />
            <p className="text-xs text-muted-foreground">
              Enter the roll number provided by the school
            </p>
          </div>
        ) : (
          <div className="space-y-2">
            <label
              htmlFor="phone"
              className="block text-sm font-medium text-foreground"
            >
              Phone Number
            </label>
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-muted-foreground">
                +91
              </span>
              <Input
                id="phone"
                type="tel"
                placeholder="9876543210"
                value={phone}
                onChange={(e): void => handlePhoneChange(e.target.value)}
                disabled={isLoading || isAuthLoading}
                aria-invalid={!!error}
                aria-describedby={error ? "form-error" : undefined}
                maxLength={10}
                inputMode="numeric"
              />
            </div>
          </div>
        )}

        {mode === "parent" ? (
          <div className="space-y-2">
            <label
              htmlFor="pin"
              className="block text-sm font-medium text-foreground"
            >
              PIN
            </label>
            <input
              id="pin"
              type="password"
              inputMode="numeric"
              placeholder="••••"
              value={pin}
              onChange={(e): void => handlePinChange(e.target.value)}
              disabled={isLoading || isAuthLoading}
              aria-invalid={!!error}
              aria-describedby={error ? "form-error" : undefined}
              maxLength={6}
              pattern="[0-9]*"
              className="flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
            />
            <p className="text-xs text-muted-foreground">
              4-6 digit PIN for parents
            </p>
          </div>
        ) : (
          <div className="space-y-2">
            <label
              htmlFor="password"
              className="block text-sm font-medium text-foreground"
            >
              Password
            </label>
            <div className="relative">
              <input
                id="password"
                type={showPassword ? "text" : "password"}
                placeholder="••••••••"
                value={password}
                onChange={(e): void => handlePasswordChange(e.target.value)}
                disabled={isLoading || isAuthLoading}
                aria-invalid={!!error}
                aria-describedby={error ? "form-error" : undefined}
                className="flex min-h-11 w-full rounded-md border border-input bg-background px-3 py-2 text-base ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                disabled={isLoading || isAuthLoading}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground disabled:opacity-50"
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? "Hide" : "Show"}
              </button>
            </div>
          </div>
        )}

        {error && (
          <p id="form-error" className="text-sm font-medium text-destructive">
            {error}
          </p>
        )}

        <Button
          type="submit"
          className="w-full h-11 min-h-11"
          disabled={isLoading || isAuthLoading}
          size="default"
        >
          {isLoading ? (
            <>
              <Spinner size="sm" />
              <span>Logging in...</span>
            </>
          ) : (
            "Login"
          )}
        </Button>

        <div className="text-center text-sm">
          {mode === "parent" ? (
            <Link
              href="/forgot-pin"
              className="text-primary hover:underline"
            >
              Forgot your PIN?
            </Link>
          ) : (
            <Link
              href="/forgot-password"
              className="text-primary hover:underline"
            >
              Forgot your password?
            </Link>
          )}
        </div>
      </form>
    </div>
  );
}
