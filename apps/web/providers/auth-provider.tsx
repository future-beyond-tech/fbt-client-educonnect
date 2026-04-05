"use client";

import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";

type RoleValue = "Parent" | "Teacher" | "Admin";

interface JwtPayload {
  sub: string;
  userId: string;
  schoolId: string;
  role: RoleValue;
  name: string;
  iat: number;
  exp: number;
}

interface User {
  userId: string;
  schoolId: string;
  role: RoleValue;
  name: string;
}

interface AuthContextType {
  token: string | null;
  user: User | null;
  isLoading: boolean;
  login: (token: string) => void;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

let accessToken: string | null = null;

export function getAuthToken(): string | null {
  return accessToken;
}

function decodeJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;

    const payloadSegment = parts[1];
    if (!payloadSegment) {
      return null;
    }

    const base64 = payloadSegment
      .replace(/-/g, "+")
      .replace(/_/g, "/")
      .padEnd(Math.ceil(payloadSegment.length / 4) * 4, "=");
    const decoded = atob(base64);
    const payload = JSON.parse(decoded) as JwtPayload;

    return payload;
  } catch {
    return null;
  }
}

export function AuthProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const login = useCallback((newToken: string): void => {
    const payload = decodeJwtPayload(newToken);
    if (!payload) {
      return;
    }

    accessToken = newToken;
    setToken(newToken);
    setUser({
      userId: payload.userId,
      schoolId: payload.schoolId,
      role: payload.role,
      name: payload.name,
    });
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

    accessToken = null;
    setToken(null);
    setUser(null);
  }, [token]);

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
        accessToken = null;
        setToken(null);
        setUser(null);
        return;
      }

      const data = (await response.json()) as { accessToken: string };
      login(data.accessToken);
    } catch {
      accessToken = null;
      setToken(null);
      setUser(null);
    }
  }, [login]);

  useEffect(() => {
    refreshToken().finally(() => {
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
