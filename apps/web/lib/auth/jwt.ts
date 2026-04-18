import type { RoleType } from "@/lib/constants";

export interface JwtPayload {
  sub: string;
  userId: string;
  schoolId: string;
  role: RoleType;
  name: string;
  must_change_password?: string | boolean;
  iat: number;
  exp: number;
}

export interface AuthUser {
  userId: string;
  schoolId: string;
  role: RoleType;
  name: string;
  mustChangePassword: boolean;
}

export function decodeJwtPayload(token: string): JwtPayload | null {
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

    return JSON.parse(decoded) as JwtPayload;
  } catch {
    return null;
  }
}

export function getUserFromToken(token: string): AuthUser | null {
  const payload = decodeJwtPayload(token);
  if (!payload) {
    return null;
  }

  const mustChange =
    payload.must_change_password === true ||
    (typeof payload.must_change_password === "string" &&
      payload.must_change_password.toLowerCase() === "true");

  return {
    userId: payload.userId,
    schoolId: payload.schoolId,
    role: payload.role,
    name: payload.name,
    mustChangePassword: mustChange,
  };
}
