// In-memory access token store. The JWT is held in a module-scoped variable
// and never written to localStorage / sessionStorage / cookies — so an XSS
// that can execute JS still cannot read the bearer token out of persistent
// storage. Persistence is provided by the HttpOnly + SameSite=Strict refresh
// cookie, which the browser alone can attach to /api/auth/refresh.
//
// Subscribers (useSyncExternalStore in AuthProvider) get notified on every
// set/clear so React state mirrors the store even when mutations originate
// from outside the provider (e.g. a 401-retry refresh in api-client).

const EARLY_REFRESH_WINDOW_MS = 30_000;

type Listener = () => void;

let accessToken: string | null = null;
let expiresAtMs: number | null = null;
const listeners = new Set<Listener>();

function notify(): void {
  for (const listener of listeners) listener();
}

export const tokenStore = {
  get(): string | null {
    return accessToken;
  },

  set(token: string, expiresInSeconds: number): void {
    accessToken = token;
    expiresAtMs = Date.now() + expiresInSeconds * 1000;
    notify();
  },

  clear(): void {
    if (accessToken === null && expiresAtMs === null) return;
    accessToken = null;
    expiresAtMs = null;
    notify();
  },

  isExpiring(): boolean {
    return expiresAtMs !== null && Date.now() > expiresAtMs - EARLY_REFRESH_WINDOW_MS;
  },

  expiresAtMs(): number | null {
    return expiresAtMs;
  },

  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return (): void => {
      listeners.delete(listener);
    };
  },
};
