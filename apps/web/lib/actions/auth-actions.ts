"use server";

import { loginSchema, type LoginInput } from "@/lib/validation/login";
import {
  clearRefreshCookie,
  forwardedRefreshCookieHeader,
  proxyRefreshCookie,
} from "@/lib/auth/refresh-cookie";

export interface LoginSuccessData {
  accessToken: string;
  expiresIn: number;
  mustChangePassword: boolean;
}

export type ActionResult<T> =
  | { ok: true; data: T }
  | { ok: false; fieldErrors?: Partial<Record<string, string>>; formError?: string };

function apiBase(): string {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }
  return baseUrl;
}

function mapBackendValidationErrors(
  body: unknown,
): { fieldErrors: Record<string, string>; formError?: string } {
  const errors: Record<string, string> = {};
  let formError: string | undefined;

  if (typeof body === "object" && body !== null) {
    const b = body as Record<string, unknown>;
    if (typeof b.detail === "string") formError = b.detail;
    else if (typeof b.title === "string") formError = b.title;

    if (b.errors && typeof b.errors === "object") {
      for (const [key, value] of Object.entries(b.errors as Record<string, unknown>)) {
        if (Array.isArray(value) && typeof value[0] === "string") {
          errors[key.toLowerCase()] = value[0] as string;
        }
      }
    }
  }

  return { fieldErrors: errors, formError };
}

export async function loginAction(
  _prev: ActionResult<LoginSuccessData> | null,
  formData: FormData,
): Promise<ActionResult<LoginSuccessData>> {
  const raw = {
    mode: formData.get("mode"),
    phone: formData.get("phone"),
    password: formData.get("password") ?? undefined,
    pin: formData.get("pin") ?? undefined,
  };

  const parsed = loginSchema.safeParse(raw);
  if (!parsed.success) {
    const fieldErrors: Record<string, string> = {};
    for (const issue of parsed.error.issues) {
      const key = issue.path[0];
      if (typeof key === "string" && !fieldErrors[key]) {
        fieldErrors[key] = issue.message;
      }
    }
    return { ok: false, fieldErrors };
  }

  const input: LoginInput = parsed.data;
  const endpoint =
    input.mode === "parent" ? "/api/auth/login-parent" : "/api/auth/login";
  const body =
    input.mode === "parent"
      ? { phone: input.phone, pin: input.pin }
      : { phone: input.phone, password: input.password };

  let response: Response;
  try {
    response = await fetch(`${apiBase()}${endpoint}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      // No credentials: this fetch runs on the Next server; cookies are
      // handled explicitly by proxyRefreshCookie() below.
    });
  } catch {
    return { ok: false, formError: "Could not reach the server. Please try again." };
  }

  if (!response.ok) {
    const errorBody = await response.json().catch(() => null);
    const mapped = mapBackendValidationErrors(errorBody);
    return {
      ok: false,
      fieldErrors: Object.keys(mapped.fieldErrors).length ? mapped.fieldErrors : undefined,
      formError:
        mapped.formError ??
        (response.status === 401
          ? "Invalid phone or password."
          : "Login failed. Please try again."),
    };
  }

  await proxyRefreshCookie(response);

  const data = (await response.json()) as LoginSuccessData;
  return { ok: true, data };
}

/**
 * Mints a short-lived backend access token inside a Server Action context
 * by calling /api/auth/refresh?noRotate=true with the browser's HttpOnly
 * refresh cookie. The noRotate flag instructs the backend to issue a fresh
 * access token WITHOUT rotating the refresh token — so two Server Actions
 * firing at the same moment don't race their rotations and trigger the
 * Phase 3 reuse-detection against themselves.
 *
 * Security posture: the rotating path in api-client.ts (browser-side
 * single-flight) continues to provide reuse-detection for cookie-theft
 * scenarios. noRotate is available only to the server-action trust
 * boundary where rotations would cause self-inflicted churn.
 *
 * Returns null if the refresh cookie is missing / invalid — the caller
 * should surface a 401 to the browser so the AuthProvider can bounce to
 * /login.
 */
export async function mintBackendAccessToken(): Promise<string | null> {
  const cookieHeader = await forwardedRefreshCookieHeader();
  if (!cookieHeader) return null;

  let response: Response;
  try {
    response = await fetch(`${apiBase()}/api/auth/refresh?noRotate=true`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Cookie: cookieHeader,
      },
    });
  } catch {
    return null;
  }

  if (!response.ok) {
    if (response.status === 401) {
      await clearRefreshCookie();
    }
    return null;
  }

  // noRotate mode doesn't emit a new Set-Cookie — nothing to proxy.
  const data = (await response.json()) as { accessToken: string };
  return data.accessToken;
}

export async function logoutAction(): Promise<{ ok: true }> {
  const cookieHeader = await forwardedRefreshCookieHeader();

  try {
    await fetch(`${apiBase()}/api/auth/logout`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(cookieHeader ? { Cookie: cookieHeader } : {}),
      },
    });
  } catch {
    // Server-side revocation is best-effort; local cookie cleanup below
    // is what actually ends the session from the browser's perspective.
  }

  await clearRefreshCookie();
  return { ok: true };
}
