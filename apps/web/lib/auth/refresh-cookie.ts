import { cookies } from "next/headers";

/**
 * Shared refresh-cookie transport helpers.
 *
 * Both the login/logout Server Actions and the /api/auth/refresh Route
 * Handler need to:
 *   1. Move the backend's Set-Cookie onto the Next.js response (so the
 *      HttpOnly cookie lives on the SAME origin the browser reads from).
 *   2. Forward the stored refresh cookie on outbound server-to-server
 *      fetches (since those calls don't automatically attach browser
 *      cookies).
 *
 * Keeping this logic in one module prevents the two transports from
 * drifting out of sync on cookie attributes — drift there is what caused
 * the logout-on-refresh bug in the first place (a cookie on origin A
 * being read by a fetch to origin B).
 */

export const REFRESH_COOKIE_NAME = "refresh_token";

export interface ParsedSetCookie {
  name: string;
  value: string;
  expires?: Date;
}

/**
 * Parses a single Set-Cookie header value into a minimal attribute map.
 * We only care about name/value and Expires — HttpOnly, Secure, SameSite
 * and Path are re-applied as app invariants via cookies().set() rather
 * than trusted from the upstream header.
 */
export function parseSetCookie(raw: string): ParsedSetCookie | null {
  const [pair, ...attrs] = raw.split(";").map((s) => s.trim());
  if (!pair) return null;
  const eq = pair.indexOf("=");
  if (eq <= 0) return null;
  const name = pair.slice(0, eq);
  const value = pair.slice(eq + 1);

  let expires: Date | undefined;
  for (const attr of attrs) {
    const parts = attr.split("=", 2).map((s) => s?.trim() ?? "");
    const k = parts[0] ?? "";
    const v = parts[1] ?? "";
    if (k.toLowerCase() === "expires" && v) {
      const d = new Date(v);
      if (!Number.isNaN(d.getTime())) expires = d;
    }
  }

  return { name, value, expires };
}

/**
 * Reads every Set-Cookie emitted by `response`, finds the refresh cookie,
 * and re-writes it onto the Next.js cookie jar with the app's canonical
 * attributes. Any other cookies the backend emits are intentionally
 * dropped — we only proxy the refresh cookie.
 *
 * The canonical attributes MUST stay aligned with
 * RefreshTokenCookieOptions.Create on the API side so the two transports
 * remain interchangeable.
 */
export async function proxyRefreshCookie(response: Response): Promise<void> {
  const setCookies = response.headers.getSetCookie();
  for (const entry of setCookies) {
    const parsed = parseSetCookie(entry);
    if (!parsed || parsed.name !== REFRESH_COOKIE_NAME) continue;

    const cookieStore = await cookies();
    cookieStore.set({
      name: parsed.name,
      value: parsed.value,
      httpOnly: true,
      secure: process.env.NODE_ENV === "production",
      sameSite: "strict",
      path: "/",
      expires: parsed.expires,
    });
  }
}

/**
 * Returns a Cookie header (`refresh_token=<value>`) suitable for a
 * server-to-server fetch, or an empty string if the cookie is absent.
 *
 * Server-side fetches don't automatically attach the browser's cookies,
 * so callers that need the backend to see the refresh token must forward
 * it explicitly.
 */
export async function forwardedRefreshCookieHeader(): Promise<string> {
  const cookieStore = await cookies();
  const refresh = cookieStore.get(REFRESH_COOKIE_NAME);
  return refresh ? `${REFRESH_COOKIE_NAME}=${refresh.value}` : "";
}

/**
 * Clears the refresh cookie from the Next.js cookie jar. Used by logout
 * paths and by the refresh endpoint when the backend rejects the token.
 */
export async function clearRefreshCookie(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(REFRESH_COOKIE_NAME);
}
