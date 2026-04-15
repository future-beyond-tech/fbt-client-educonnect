const TOKEN_STORAGE_KEY = "auth_access_token";

// Initialise from localStorage so the token survives page refreshes.
// Guard against SSR environments where `window` is unavailable.
let accessToken: string | null =
  typeof window !== "undefined"
    ? localStorage.getItem(TOKEN_STORAGE_KEY)
    : null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
  if (typeof window !== "undefined") {
    if (token) {
      localStorage.setItem(TOKEN_STORAGE_KEY, token);
    } else {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
    }
  }
}

export function clearAccessToken(): void {
  accessToken = null;
  if (typeof window !== "undefined") {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
  }
}
