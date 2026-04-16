"use client";

import * as React from "react";
import Link from "next/link";
import { Eye, EyeOff, KeyRound, UserRound } from "lucide-react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS, defaultRouteByRole } from "@/lib/constants";
import {
  isValidJapanPhone,
  JAPAN_PHONE_COUNTRY_CODE,
  JAPAN_PHONE_COUNTRY_LABEL,
  JAPAN_PHONE_LOCAL_DIGITS,
  JAPAN_PHONE_VALIDATION_MESSAGE,
  normalizeJapanPhoneInput,
} from "@/lib/phone";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";

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
  const staffPhoneRef = React.useRef<HTMLInputElement | null>(null);
  const secureFieldClassName =
    "focus-ring flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 text-sm text-foreground shadow-[0_12px_30px_-28px_rgba(15,23,42,0.38)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-60";

  React.useEffect(() => {
    if (!isAuthLoading && user) {
      router.replace(defaultRouteByRole[user.role]);
    }
  }, [isAuthLoading, router, user]);

  const handlePhoneChange = (value: string): void => {
    setPhone(normalizeJapanPhoneInput(value));
    setError("");
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
      if (!isValidJapanPhone(phone)) {
        setError(JAPAN_PHONE_VALIDATION_MESSAGE);
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
    <div className="space-y-6">
      {isAuthLoading && (
        <StatusBanner variant="info">
          Restoring your previous session...
        </StatusBanner>
      )}
      <div className="space-y-2">
        <p className="text-sm font-semibold text-foreground">Sign in as</p>
        <div
          role="tablist"
          aria-label="Login type"
          className="rounded-[22px] border border-border/70 bg-card/70 p-1.5 shadow-[0_14px_34px_-30px_rgba(15,23,42,0.4)] backdrop-blur-sm dark:bg-card/86"
        >
          <div className="grid grid-cols-2 gap-1.5">
            <button
              type="button"
              role="tab"
              aria-selected={mode === "parent"}
              onClick={() => handleModeSwitch("parent")}
              disabled={isLoading || isAuthLoading}
              className={[
                "focus-ring tap-target inline-flex items-center justify-center gap-2 rounded-[18px] px-3 text-sm font-semibold transition",
                mode === "parent"
                  ? "bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--accent)))] text-primary-foreground shadow-[0_16px_36px_-22px_rgba(18,66,145,0.65)]"
                  : "text-muted-foreground hover:bg-card/60 hover:text-foreground",
              ].join(" ")}
            >
              <UserRound className="h-4 w-4" aria-hidden="true" />
              Parent
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === "staff"}
              onClick={() => handleModeSwitch("staff")}
              disabled={isLoading || isAuthLoading}
              className={[
                "focus-ring tap-target inline-flex items-center justify-center gap-2 rounded-[18px] px-3 text-sm font-semibold transition",
                mode === "staff"
                  ? "bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--accent)))] text-primary-foreground shadow-[0_16px_36px_-22px_rgba(18,66,145,0.65)]"
                  : "text-muted-foreground hover:bg-card/60 hover:text-foreground",
              ].join(" ")}
            >
              <KeyRound className="h-4 w-4" aria-hidden="true" />
              Staff
            </button>
          </div>
        </div>
        <p className="text-xs leading-5 text-muted-foreground">
          {mode === "parent"
            ? "Use your child’s roll number and parent PIN."
            : "Use your phone number and password."}
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        {mode === "parent" ? (
          <Input
            id="rollNumber"
            label="Roll number"
            type="text"
            placeholder="e.g. 2024001"
            value={rollNumber}
            onChange={(e): void => handleRollNumberChange(e.target.value)}
            disabled={isLoading || isAuthLoading}
            aria-invalid={!!error}
            aria-describedby={error ? "form-error" : undefined}
            autoComplete="username"
            inputMode="numeric"
            hint="As provided by the school."
          />
        ) : (
          <div className="space-y-2">
            <label htmlFor="phone" className="block text-sm font-medium text-foreground">
              Phone number
            </label>
            <div
              className="focus-within:ring-ring focus-within:ring-offset-background flex min-h-12 items-center gap-3 rounded-[20px] border border-input/90 bg-card/85 px-4 shadow-[0_12px_30px_-28px_rgba(15,23,42,0.38)] ring-offset-background backdrop-blur-sm focus-within:border-primary/40 focus-within:ring-2 focus-within:ring-offset-2"
              onClick={() => staffPhoneRef.current?.focus()}
              role="group"
              aria-labelledby="phone"
            >
              <span className="inline-flex items-center gap-2 rounded-full border border-border/70 bg-card/70 px-3 py-1 text-sm font-semibold text-foreground/90">
                <span className="text-muted-foreground">{JAPAN_PHONE_COUNTRY_LABEL}</span>
                <span className="h-4 w-px bg-border/70" aria-hidden="true" />
                {JAPAN_PHONE_COUNTRY_CODE}
              </span>
              <input
                id="phone"
                type="tel"
                placeholder="Enter 11-digit number"
                value={phone}
                onChange={(e): void => handlePhoneChange(e.target.value)}
                disabled={isLoading || isAuthLoading}
                aria-invalid={!!error}
                aria-describedby={error ? "form-error" : "staff-phone-hint"}
                maxLength={JAPAN_PHONE_LOCAL_DIGITS}
                inputMode="numeric"
                pattern="[0-9]*"
                autoComplete="tel-national"
                enterKeyHint="done"
                ref={staffPhoneRef}
                className="w-full flex-1 bg-transparent py-3 text-sm font-semibold text-foreground outline-none placeholder:font-medium placeholder:text-muted-foreground/90"
              />
            </div>
            <p id="staff-phone-hint" className="text-xs text-muted-foreground">
              Use the 11-digit number registered with the school.
            </p>
          </div>
        )}

        {mode === "parent" ? (
          <div className="space-y-2">
            <label htmlFor="pin" className="block text-sm font-medium text-foreground">
              Parent PIN
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
              autoComplete="current-password"
              className={secureFieldClassName}
            />
            <p className="text-xs text-muted-foreground">4–6 digits</p>
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
                aria-describedby={error ? "form-error" : "staff-password-hint"}
                autoComplete="current-password"
                className={secureFieldClassName}
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                disabled={isLoading || isAuthLoading}
                className="focus-ring tap-target absolute right-2 top-1/2 -translate-y-1/2 inline-flex items-center justify-center rounded-full px-3 text-sm font-semibold text-muted-foreground hover:bg-card/60 hover:text-foreground disabled:opacity-50"
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? (
                  <>
                    <EyeOff className="h-4 w-4" aria-hidden="true" />
                    <span className="sr-only">Hide</span>
                  </>
                ) : (
                  <>
                    <Eye className="h-4 w-4" aria-hidden="true" />
                    <span className="sr-only">Show</span>
                  </>
                )}
              </button>
            </div>
            <p id="staff-password-hint" className="text-xs text-muted-foreground">
              Use your staff account password.
            </p>
          </div>
        )}

        {error && (
          <StatusBanner id="form-error" variant="error">
            {error}
          </StatusBanner>
        )}

        <Button
          type="submit"
          className="h-12 min-h-12 w-full"
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

        <div className="pt-1 text-center text-sm">
          {mode === "parent" ? (
            <Link
              href="/forgot-pin"
              className="focus-ring rounded-full px-3 py-2 font-semibold text-primary hover:underline"
            >
              Forgot your PIN?
            </Link>
          ) : (
            <Link
              href="/forgot-password"
              className="focus-ring rounded-full px-3 py-2 font-semibold text-primary hover:underline"
            >
              Forgot your password?
            </Link>
          )}
        </div>
      </form>
    </div>
  );
}
