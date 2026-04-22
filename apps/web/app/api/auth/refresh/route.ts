import { NextResponse } from "next/server";
import {
  forwardedRefreshCookieHeader,
  proxyRefreshCookie,
  clearRefreshCookie,
} from "@/lib/auth/refresh-cookie";

/**
 * Same-origin refresh proxy.
 *
 * The browser fetches this endpoint (not the backend directly) so the
 * HttpOnly `refresh_token` cookie is attached automatically: Next.js
 * writes the cookie onto its own origin at login, so the browser sees
 * it as "first-party" when hitting /api/auth/refresh on the same
 * origin.
 *
 * This handler then forwards the cookie over a server-to-server call to
 * the real API (where the refresh secret and DB live), proxies the new
 * `Set-Cookie: refresh_token=...` back onto the Next.js origin via the
 * shared helper, and returns the JSON payload to the browser.
 *
 * Why not have the browser hit the backend directly? Cookies set by
 * Next.js's cookies().set() are host-only to the Next.js origin. A
 * browser fetch to a different origin (the API) would not attach them,
 * so every refresh would arrive cookieless and return 401 — which was
 * exactly the logout-on-refresh bug this proxy fixes.
 */

function apiBaseUrl(): string {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }
  return baseUrl;
}

export const dynamic = "force-dynamic";
export const runtime = "nodejs";

export async function POST(): Promise<NextResponse> {
  const cookieHeader = await forwardedRefreshCookieHeader();
  if (!cookieHeader) {
    return NextResponse.json(
      { title: "Unauthorized", status: 401 },
      { status: 401 },
    );
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${apiBaseUrl()}/api/auth/refresh`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Cookie: cookieHeader,
      },
      // Server-to-server: no browser credentials, cookie is forwarded
      // explicitly above.
      cache: "no-store",
    });
  } catch {
    return NextResponse.json(
      { title: "Upstream unavailable", status: 502 },
      { status: 502 },
    );
  }

  if (!upstream.ok) {
    if (upstream.status === 401) {
      // Backend rejected the refresh — cookie is dead. Clear it so the
      // next boot doesn't retry endlessly with the same stale value.
      await clearRefreshCookie();
    }
    const body = await upstream.json().catch(() => ({
      title: upstream.statusText || "Refresh failed",
      status: upstream.status,
    }));
    return NextResponse.json(body, { status: upstream.status });
  }

  // Proxy the rotated refresh cookie onto the Next.js origin BEFORE
  // reading the body so we don't accidentally swallow the Set-Cookie on
  // a response-body parse error.
  await proxyRefreshCookie(upstream);

  const payload = (await upstream.json()) as {
    accessToken: string;
    expiresIn: number;
    mustChangePassword: boolean;
  };

  return NextResponse.json(payload, { status: 200 });
}
