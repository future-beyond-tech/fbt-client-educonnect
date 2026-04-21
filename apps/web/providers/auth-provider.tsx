"use client";

import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import type { AuthUser } from "@/lib/auth/jwt";
import { getUserFromToken, secondsUntilExpiry } from "@/lib/auth/jwt";
import { tokenStore } from "@/lib/auth/token-store";
import { refreshAccessToken } from "@/lib/api-client";

interface AuthContextType {
  token: string | null;
  user: AuthUser | null;
  isLoading: boolean;
  // Accepts a token from /api/auth/login or /api/auth/login-parent.
  // expiresInSeconds is optional — falls back to decoding the JWT exp claim
  // so existing callers don't have to change signature.
  login: (token: string, expiresInSeconds?: number) => void;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

const AUTH_CHANNEL_NAME = "educonnect-auth";
type AuthBroadcastMessage = { type: "logout" };

function subscribeToTokenStore(listener: () => void): () => void {
  return tokenStore.subscribe(listener);
}

function getSnapshot(): string | null {
  return tokenStore.get();
}

function getServerSnapshot(): string | null {
  // SSR never has an in-memory token — hydration always starts unauthenticated.
  return null;
}

export function AuthProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  // Token state is mirrored from the module-scoped tokenStore via
  // useSyncExternalStore. Writes from anywhere (login, logout, 401-retry
  // refresh inside api-client) propagate here without needing a setState call
  // threaded through.
  const token = useSyncExternalStore(subscribeToTokenStore, getSnapshot, getServerSnapshot);

  const [isLoading, setIsLoading] = useState<boolean>(true);
  const user = useMemo<AuthUser | null>(
    () => (token ? getUserFromToken(token) : null),
    [token]
  );

  const broadcastRef = useRef<BroadcastChannel | null>(null);

  const login = useCallback((newToken: string, expiresInSeconds?: number): void => {
    const nextUser = getUserFromToken(newToken);
    if (!nextUser) return;
    const ttl = expiresInSeconds ?? secondsUntilExpiry(newToken) ?? 0;
    if (ttl <= 0) return;
    tokenStore.set(newToken, ttl);
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    // Best-effort server logout; local state still cleared on failure.
    const bearer = tokenStore.get();
    try {
      await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/auth/logout`, {
        method: "POST",
        credentials: "include",
        headers: bearer
          ? { Authorization: `Bearer ${bearer}`, "Content-Type": "application/json" }
          : { "Content-Type": "application/json" },
      });
    } catch {
      // Ignore — we still clean up locally.
    }

    tokenStore.clear();

    // Tell peer tabs their session is gone too.
    broadcastRef.current?.postMessage({ type: "logout" } satisfies AuthBroadcastMessage);
  }, []);

  const refreshToken = useCallback(async (): Promise<void> => {
    await refreshAccessToken();
  }, []);

  // Bootstrap: on mount, ask the server for a fresh access token using the
  // HttpOnly refresh cookie. If the cookie is missing/invalid the call
  // returns null and the user stays logged out.
  useEffect(() => {
    let cancelled = false;
    (async (): Promise<void> => {
      await refreshAccessToken();
      if (!cancelled) setIsLoading(false);
    })();
    return (): void => {
      cancelled = true;
    };
  }, []);

  // Pre-emptive refresh: schedule a refresh 120s before expiry so the token
  // is fresh when the user clicks something.
  useEffect(() => {
    if (!token) return;
    const remaining = secondsUntilExpiry(token);
    if (remaining === null) return;

    const refreshAtMs = Math.max((remaining - 120) * 1000, 0);
    const timer = setTimeout(() => {
      void refreshAccessToken();
    }, refreshAtMs);

    return (): void => {
      clearTimeout(timer);
    };
  }, [token]);

  // Cross-tab logout: if another tab logs out (or its refresh fails), clear
  // this tab's token too. BroadcastChannel isn't available on every browser
  // the app supports — the null check keeps older runtimes working.
  useEffect(() => {
    if (typeof window === "undefined" || typeof BroadcastChannel === "undefined") {
      return undefined;
    }
    const channel = new BroadcastChannel(AUTH_CHANNEL_NAME);
    broadcastRef.current = channel;
    channel.onmessage = (event: MessageEvent<AuthBroadcastMessage>): void => {
      if (event.data?.type === "logout") {
        tokenStore.clear();
      }
    };
    return (): void => {
      channel.close();
      broadcastRef.current = null;
    };
  }, []);

  const value: AuthContextType = useMemo(
    () => ({
      token,
      user,
      isLoading,
      login,
      logout,
      refreshToken,
    }),
    [token, user, isLoading, login, logout, refreshToken]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
