import { getAccessToken } from "@/lib/auth/session";

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

async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }

  const url = `${baseUrl}${endpoint}`;
  const token = getAccessToken();

  const headers: HeadersInit = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  if (token) {
    (headers as Record<string, string>).Authorization = `Bearer ${token}`;
  }

  try {
    const response = await fetch(url, {
      ...options,
      headers,
      credentials: "include",
    });

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

    if (response.status === 204) {
      return undefined as T;
    }

    if (!isJson) {
      return undefined as T;
    }

    const data = (await response.json()) as T;
    return data;
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }

    if (error instanceof TypeError) {
      throw new ApiError(0, `Network error: ${error.message}`);
    }

    throw new ApiError(500, "An unexpected error occurred");
  }
}

export async function apiGet<T>(endpoint: string): Promise<T> {
  return apiRequest<T>(endpoint, {
    method: "GET",
  });
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
  return apiRequest<T>(endpoint, {
    method: "DELETE",
  });
}

/**
 * Multipart/form-data POST. Used for CSV uploads — we drop the JSON
 * Content-Type so the browser can pick a boundary for the FormData body.
 */
export async function apiPostMultipart<T>(
  endpoint: string,
  formData: FormData
): Promise<T> {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }

  const url = `${baseUrl}${endpoint}`;
  const token = getAccessToken();

  const headers: Record<string, string> = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(url, {
    method: "POST",
    headers,
    body: formData,
    credentials: "include",
  });

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
