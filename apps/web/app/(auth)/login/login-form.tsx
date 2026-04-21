"use client";

import * as React from "react";
import Link from "next/link";
import { Eye, EyeOff, KeyRound, UserRound } from "lucide-react";
import { useActionState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { defaultRouteByRole } from "@/lib/constants";
import {
  JAPAN_PHONE_COUNTRY_CODE,
  JAPAN_PHONE_COUNTRY_LABEL,
  JAPAN_PHONE_LOCAL_DIGITS,
  normalizeJapanPhoneInput,
} from "@/lib/phone";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { StatusBanner } from "@/components/shared/status-banner";
import {
  loginAction,
  type ActionResult,
  type LoginSuccessData,
} from "@/lib/actions/auth-actions";

type LoginMode = "parent" | "staff";

const initialState: ActionResult<LoginSuccessData> | null = null;

export function LoginForm(): React.ReactElement {
  const router = useRouter();
  const { login, user, isLoading: isAuthLoading } = useAuth();
  const [mode, setMode] = React.useState<LoginMode>("parent");
  const [phone, setPhone] = React.useState<string>("");
  const [pin, setPin] = React.useState<string>("");
  const [password, setPassword] = React.useState<string>("");
  const [showPassword, setShowPassword] = React.useState<boolean>(false);
  const phoneRef = React.useRef<HTMLInputElement | null>(null);
  const secureFieldClassName =
    "focus-ring flex min-h-12 w-full rounded-[20px] border border-input/90 bg-card/85 px-4 py-3 text-sm text-foreground shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] ring-offset-background backdrop-blur-sm placeholder:text-muted-foreground/90 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-60";

  const [state, formAction, isPending] = useActionState(loginAction, initialState);

  // Redirect an already-authenticated visitor to their dashboard.
  React.useEffect(() => {
    if (isAuthLoading || !user) return;
    if (user.mustChangePassword) {
      router.replace(user.role === "Parent" ? "/change-pin" : "/change-password");
      return;
    }
    router.replace(defaultRouteByRole[user.role]);
  }, [isAuthLoading, router, user]);

  // Promote a successful server-action login to in-memory token state so
  // subsequent api-client requests carry the bearer.
  React.useEffect(() => {
    if (state?.ok) {
      login(state.data.accessToken, state.data.expiresIn);
    }
  }, [state, login]);

  const handlePhoneChange = (value: string): void => {
    setPhone(normalizeJapanPhoneInput(value));
  };

  const handlePinChange = (value: string): void => {
    const cleaned = value.replace(/\D/g, "");
    if (cleaned.length <= 6) {
      setPin(cleaned);
    }
  };

  const handleModeSwitch = (newMode: LoginMode): void => {
    setMode(newMode);
    setPhone("");
    setPin("");
    setPassword("");
  };

  const formError = state && !state.ok ? state.formError : undefined;
  const fieldErrors = state && !state.ok ? state.fieldErrors ?? {} : {};
  const phoneError = fieldErrors.phone;
  const passwordError = fieldErrors.password;
  const pinError = fieldErrors.pin;

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
          className="rounded-[22px] border border-border/70 bg-card/70 p-1.5 shadow-[0_14px_34px_-30px_rgba(15,40,69,0.4)] backdrop-blur-sm dark:bg-card/86"
        >
          <div className="grid grid-cols-2 gap-1.5">
            <button
              type="button"
              role="tab"
              aria-selected={mode === "parent"}
              onClick={() => handleModeSwitch("parent")}
              disabled={isPending || isAuthLoading}
              className={[
                "focus-ring tap-target inline-flex items-center justify-center gap-2 rounded-[18px] px-3 text-sm font-semibold transition",
                mode === "parent"
                  ? "rainbow-bg text-white shadow-[0_16px_36px_-22px_rgba(15,40,69,0.55)]"
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
              disabled={isPending || isAuthLoading}
              className={[
                "focus-ring tap-target inline-flex items-center justify-center gap-2 rounded-[18px] px-3 text-sm font-semibold transition",
                mode === "staff"
                  ? "rainbow-bg text-white shadow-[0_16px_36px_-22px_rgba(15,40,69,0.55)]"
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
            ? "Use your phone number and parent PIN."
            : "Use your phone number and password."}
        </p>
      </div>

      <form action={formAction} className="space-y-4">
        <input type="hidden" name="mode" value={mode} />

        <div className="space-y-2">
          <label htmlFor="phone" className="block text-sm font-medium text-foreground">
            Phone number
          </label>
          <div
            className="focus-within:ring-ring focus-within:ring-offset-background flex min-h-12 items-center gap-3 rounded-[20px] border border-input/90 bg-card/85 px-4 shadow-[0_12px_30px_-28px_rgba(15,40,69,0.38)] ring-offset-background backdrop-blur-sm focus-within:border-primary/40 focus-within:ring-2 focus-within:ring-offset-2"
            onClick={() => phoneRef.current?.focus()}
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
              name="phone"
              type="tel"
              placeholder="Enter 11-digit number"
              value={phone}
              onChange={(e): void => handlePhoneChange(e.target.value)}
              disabled={isPending || isAuthLoading}
              aria-invalid={!!(formError || phoneError)}
              aria-describedby={phoneError ? "phone-error" : mode === "parent" ? "parent-phone-hint" : "staff-phone-hint"}
              maxLength={JAPAN_PHONE_LOCAL_DIGITS}
              inputMode="numeric"
              pattern="[0-9]*"
              autoComplete="tel-national"
              enterKeyHint="done"
              ref={phoneRef}
              className="w-full flex-1 bg-transparent py-3 text-sm font-semibold text-foreground outline-none placeholder:font-medium placeholder:text-muted-foreground/90"
            />
          </div>
          {phoneError ? (
            <p id="phone-error" className="text-xs text-destructive">{phoneError}</p>
          ) : (
            <p
              id={mode === "parent" ? "parent-phone-hint" : "staff-phone-hint"}
              className="text-xs text-muted-foreground"
            >
              {mode === "parent"
                ? "Use the phone number linked to your parent account."
                : "Use the 11-digit number registered with the school."}
            </p>
          )}
        </div>

        {mode === "parent" ? (
          <div className="space-y-2">
            <label htmlFor="pin" className="block text-sm font-medium text-foreground">
              Parent PIN
            </label>
            <input
              id="pin"
              name="pin"
              type="password"
              inputMode="numeric"
              placeholder="••••"
              value={pin}
              onChange={(e): void => handlePinChange(e.target.value)}
              disabled={isPending || isAuthLoading}
              aria-invalid={!!(formError || pinError)}
              aria-describedby={pinError ? "pin-error" : undefined}
              maxLength={6}
              pattern="[0-9]*"
              autoComplete="current-password"
              className={secureFieldClassName}
            />
            {pinError ? (
              <p id="pin-error" className="text-xs text-destructive">{pinError}</p>
            ) : (
              <p className="text-xs text-muted-foreground">4–6 digits</p>
            )}
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
                name="password"
                type={showPassword ? "text" : "password"}
                placeholder="••••••••"
                value={password}
                onChange={(e): void => setPassword(e.target.value)}
                disabled={isPending || isAuthLoading}
                aria-invalid={!!(formError || passwordError)}
                aria-describedby={passwordError ? "password-error" : "staff-password-hint"}
                autoComplete="current-password"
                className={secureFieldClassName}
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                disabled={isPending || isAuthLoading}
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
            {passwordError ? (
              <p id="password-error" className="text-xs text-destructive">{passwordError}</p>
            ) : (
              <p id="staff-password-hint" className="text-xs text-muted-foreground">
                Use your staff account password.
              </p>
            )}
          </div>
        )}

        {formError && (
          <StatusBanner id="form-error" variant="error">
            {formError}
          </StatusBanner>
        )}

        <Button
          type="submit"
          className="h-12 min-h-12 w-full"
          disabled={isPending || isAuthLoading}
          size="default"
        >
          {isPending ? (
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
