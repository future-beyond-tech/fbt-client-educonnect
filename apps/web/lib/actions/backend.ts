"use server";

import { mintBackendAccessToken } from "@/lib/actions/auth-actions";

// Shared-surface result type for every Server Action that calls the .NET API.
// Actions map this into their own feature-specific success/error shape before
// returning to the client — the client never sees callBackend's internals.
export type BackendResult<T> =
  | { ok: true; data: T }
  | {
      ok: false;
      status: number;
      // Preserves the .NET ProblemDetails body so callers can map 400 field
      // errors into their own validation shape.
      problem?: unknown;
    };

function apiBase(): string {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }
  return baseUrl;
}

/**
 * Authenticated backend call from inside a Server Action. Mints a short-lived
 * bearer via mintBackendAccessToken (non-rotating refresh), then performs
 * exactly one request. No automatic retry — if the token mint fails or the
 * backend returns 401, the caller surfaces the failure and the client
 * bounces the user to /login.
 */
export async function callBackend<T>(
  path: string,
  init: RequestInit = {},
): Promise<BackendResult<T>> {
  const token = await mintBackendAccessToken();
  if (!token) {
    return { ok: false, status: 401 };
  }

  let response: Response;
  try {
    response = await fetch(`${apiBase()}${path}`, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        ...(init.headers ?? {}),
        Authorization: `Bearer ${token}`,
      },
    });
  } catch {
    return { ok: false, status: 0 };
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => undefined);
    return { ok: false, status: response.status, problem };
  }

  if (response.status === 204) {
    return { ok: true, data: undefined as T };
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) {
    return { ok: true, data: undefined as T };
  }

  const data = (await response.json()) as T;
  return { ok: true, data };
}

/**
 * Convenience mapper: turn a BackendResult failure into a generic form
 * error string, extracting the .NET ProblemDetails title/detail when
 * available. Feature actions typically want to pull field errors out of
 * problem.errors themselves, so this is only a last-resort formatter.
 */
export async function formErrorFromBackend(result: BackendResult<unknown>): Promise<string> {
  if (result.ok) return "";
  if (result.status === 0) return "Could not reach the server. Please try again.";
  if (result.status === 401) return "Your session has expired. Please sign in again.";

  const problem = result.problem as
    | { detail?: string; title?: string; errors?: Record<string, string[]> }
    | undefined;
  if (problem?.detail) return problem.detail;
  if (problem?.title) return problem.title;
  return `Request failed (HTTP ${result.status}).`;
}

/**
 * Convenience mapper: lift ProblemDetails.errors into a
 * { fieldName: firstMessage } map for action return shapes.
 */
export async function fieldErrorsFromBackend(
  result: BackendResult<unknown>,
): Promise<Record<string, string>> {
  if (result.ok) return {};
  const problem = result.problem as
    | { errors?: Record<string, string[]> }
    | undefined;
  const errors: Record<string, string> = {};
  if (problem?.errors) {
    for (const [key, values] of Object.entries(problem.errors)) {
      if (Array.isArray(values) && typeof values[0] === "string") {
        errors[key[0]?.toLowerCase() + key.slice(1)] = values[0];
      }
    }
  }
  return errors;
}
