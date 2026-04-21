import { tokenStore } from "@/lib/auth/token-store";

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  traceId: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    public statusCode: number,
    message: string,
    public details?: ProblemDetails
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * True when the API rejected the call because the authenticated user still has
 * `must_change_password=true` on their JWT. The server returns HTTP 403 with
 * `errors.code = ["MUST_CHANGE_PASSWORD"]`.
 */
export function isMustChangePasswordError(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false;
  if (error.statusCode !== 403) return false;
  const code = error.details?.errors?.code;
  return Array.isArray(code) && code.includes("MUST_CHANGE_PASSWORD");
}

export interface RefreshResult {
  accessToken: string;
  expiresIn: number;
  mustChangePassword: boolean;
}

// Single-flight refresh: concurrent callers share the same in-flight fetch so
// we never fire more than one /auth/refresh request at a time. This prevents
// a burst of 401s (e.g. a dashboard doing parallel fetches right after token
// expiry) from racing and revoking each other's refresh tokens.
let refreshInFlight: Promise<RefreshResult | null> | null = null;

function apiBaseUrl(): string {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }
  return baseUrl;
}

async function doRefresh(): Promise<RefreshResult | null> {
  try {
    const response = await fetch(`${apiBaseUrl()}/api/auth/refresh`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
    });

    if (!response.ok) {
      tokenStore.clear();
      return null;
    }

    const data = (await response.json()) as RefreshResult;
    tokenStore.set(data.accessToken, data.expiresIn);
    return data;
  } catch {
    tokenStore.clear();
    return null;
  }
}

/**
 * Request a fresh access token using the HttpOnly refresh cookie. Concurrent
 * calls coalesce into a single network request. Returns the new token
 * metadata on success, or null if the refresh failed (caller must re-login).
 */
export async function refreshAccessToken(): Promise<RefreshResult | null> {
  if (refreshInFlight) return refreshInFlight;
  refreshInFlight = doRefresh().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

interface RequestOptions extends RequestInit {
  // Internal: set to true on the second attempt after a 401 → refresh so we
  // don't recursively refresh on repeated 401s.
  _retried?: boolean;
}

async function apiRequest<T>(
  endpoint: string,
  options: RequestOptions = {}
): Promise<T> {
  const url = `${apiBaseUrl()}${endpoint}`;
  const token = tokenStore.get();

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(url, {
      ...options,
      headers,
      credentials: "include",
    });
  } catch (error) {
    if (error instanceof TypeError) {
      throw new ApiError(0, `Network error: ${error.message}`);
    }
    throw new ApiError(500, "An unexpected error occurred");
  }

  // On 401 for an authenticated request, try exactly one refresh + retry.
  // /api/auth/refresh itself is excluded so we don't loop, and retried
  // requests are also excluded (see _retried).
  const isAuthRefresh = endpoint === "/api/auth/refresh";
  if (
    response.status === 401 &&
    !options._retried &&
    !isAuthRefresh &&
    token !== null
  ) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      return apiRequest<T>(endpoint, { ...options, _retried: true });
    }
    // Refresh failed — tokenStore already cleared. Fall through so the
    // caller sees the 401 and can bounce to /login.
  }

  const contentType = response.headers.get("content-type");
  const isJson = contentType?.includes("application/json");

  if (!response.ok) {
    let problemDetails: ProblemDetails | undefined;

    if (isJson) {
      try {
        const errorBody = (await response.json()) as Record<string, unknown>;
        if ("title" in errorBody && "status" in errorBody) {
          problemDetails = errorBody as unknown as ProblemDetails;
        }
      } catch {
        // JSON parsing failed, use status text
      }
    }

    const errorMessage =
      problemDetails?.detail ?? problemDetails?.title ?? response.statusText;

    throw new ApiError(response.status, errorMessage, problemDetails);
  }

  if (response.status === 204 || !isJson) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function apiGet<T>(endpoint: string): Promise<T> {
  return apiRequest<T>(endpoint, { method: "GET" });
}

export async function apiPost<T>(
  endpoint: string,
  body: unknown = {}
): Promise<T> {
  return apiRequest<T>(endpoint, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export async function apiPut<T>(
  endpoint: string,
  body: unknown = {}
): Promise<T> {
  return apiRequest<T>(endpoint, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export async function apiDelete<T>(endpoint: string): Promise<T> {
  return apiRequest<T>(endpoint, { method: "DELETE" });
}

/**
 * Multipart/form-data POST. Used for CSV uploads — we drop the JSON
 * Content-Type so the browser can pick a boundary for the FormData body.
 * Shares the single-flight refresh machinery via refreshAccessToken().
 */
export async function apiPostMultipart<T>(
  endpoint: string,
  formData: FormData,
  retried = false
): Promise<T> {
  const url = `${apiBaseUrl()}${endpoint}`;
  const token = tokenStore.get();

  const headers: Record<string, string> = {};
  if (token) headers.Authorization = `Bearer ${token}`;

  const response = await fetch(url, {
    method: "POST",
    headers,
    body: formData,
    credentials: "include",
  });

  if (response.status === 401 && !retried && token !== null) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      return apiPostMultipart<T>(endpoint, formData, true);
    }
  }

  const contentType = response.headers.get("content-type");
  const isJson = contentType?.includes("application/json");

  if (!response.ok) {
    let problemDetails: ProblemDetails | undefined;
    if (isJson) {
      try {
        const errorBody = (await response.json()) as Record<string, unknown>;
        if ("title" in errorBody && "status" in errorBody) {
          problemDetails = errorBody as unknown as ProblemDetails;
        }
      } catch {
        // ignored
      }
    }
    const errorMessage =
      problemDetails?.detail ?? problemDetails?.title ?? response.statusText;
    throw new ApiError(response.status, errorMessage, problemDetails);
  }

  if (response.status === 204 || !isJson) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
