"use client";

import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import type { AuthUser } from "@/lib/auth/jwt";
import { decodeJwtPayload, getUserFromToken } from "@/lib/auth/jwt";
import { clearAccessToken, getAccessToken, setAccessToken } from "@/lib/auth/session";

interface AuthContextType {
  token: string | null;
  user: AuthUser | null;
  isLoading: boolean;
  login: (token: string) => void;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  // True only while the initial session-restore refresh is in-flight.
  // Used to prevent a failing restore from wiping a concurrent manual login.
  const isInitialRefreshRef = useRef<boolean>(true);

  const clearAuthState = useCallback((): void => {
    clearAccessToken();
    setToken(null);
    setUser(null);
  }, []);

  const login = useCallback((newToken: string): void => {
    const nextUser = getUserFromToken(newToken);
    if (!nextUser) {
      return;
    }

    setAccessToken(newToken);
    setToken(newToken);
    setUser(nextUser);
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    try {
      await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/auth/logout`, {
        method: "POST",
        credentials: "include",
        headers: token
          ? {
              Authorization: `Bearer ${token}`,
              "Content-Type": "application/json",
            }
          : { "Content-Type": "application/json" },
      });
    } catch {
      // Logout API failure should not prevent local cleanup
    }

    clearAuthState();
  }, [clearAuthState, token]);

  const refreshToken = useCallback(async (): Promise<void> => {
    try {
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL}/api/auth/refresh`,
        {
          method: "POST",
          credentials: "include",
          headers: {
            "Content-Type": "application/json",
          },
        }
      );

      if (!response.ok) {
        // Guard: if this is the initial session restore and the user manually
        // logged in while the request was in-flight, do not wipe their session.
        if (!isInitialRefreshRef.current || !getAccessToken()) {
          clearAuthState();
        }
        return;
      }

      const data = (await response.json()) as { accessToken: string };
      login(data.accessToken);
    } catch {
      if (!isInitialRefreshRef.current || !getAccessToken()) {
        clearAuthState();
      }
    }
  }, [clearAuthState, login]);

  useEffect(() => {
    refreshToken().finally(() => {
      isInitialRefreshRef.current = false;
      setIsLoading(false);
    });
  }, [refreshToken]);

  useEffect(() => {
    if (!token) return;

    const payload = decodeJwtPayload(token);
    if (!payload) return;

    const now = Math.floor(Date.now() / 1000);
    const timeUntilExpiry = payload.exp - now;
    const refreshAt = Math.max((timeUntilExpiry - 120) * 1000, 0);

    const timer = setTimeout(() => {
      refreshToken();
    }, refreshAt);

    return (): void => {
      clearTimeout(timer);
    };
  }, [token, refreshToken]);

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
