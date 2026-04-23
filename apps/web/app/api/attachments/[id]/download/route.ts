import { NextResponse } from "next/server";
import {
  clearRefreshCookie,
  forwardedRefreshCookieHeader,
} from "@/lib/auth/refresh-cookie";

function apiBaseUrl(): string {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!baseUrl) {
    throw new Error("NEXT_PUBLIC_API_URL is not defined");
  }
  return baseUrl;
}

async function mintBackendAccessToken(): Promise<string | null> {
  const cookieHeader = await forwardedRefreshCookieHeader();
  if (!cookieHeader) {
    return null;
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}/api/auth/refresh?noRotate=true`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Cookie: cookieHeader,
      },
      cache: "no-store",
    });
  } catch {
    return null;
  }

  if (!response.ok) {
    if (response.status === 401) {
      await clearRefreshCookie();
    }
    return null;
  }

  const data = (await response.json()) as { accessToken: string };
  return data.accessToken;
}

function passthroughHeaders(upstream: Response): HeadersInit {
  const headers = new Headers();
  for (const name of [
    "content-type",
    "content-length",
    "content-disposition",
    "cache-control",
  ]) {
    const value = upstream.headers.get(name);
    if (value) {
      headers.set(name, value);
    }
  }
  return headers;
}

export const dynamic = "force-dynamic";
export const runtime = "nodejs";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
): Promise<NextResponse> {
  const { id } = await params;
  const token = await mintBackendAccessToken();

  if (!token) {
    return NextResponse.json(
      { title: "Unauthorized", status: 401 },
      { status: 401 }
    );
  }

  let upstream: Response;
  try {
    const upstreamUrl = new URL(`${apiBaseUrl()}/api/attachments/${id}/download`);
    const requestUrl = new URL(request.url);
    for (const [key, value] of requestUrl.searchParams.entries()) {
      upstreamUrl.searchParams.append(key, value);
    }

    upstream = await fetch(upstreamUrl, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      cache: "no-store",
      redirect: "manual",
    });
  } catch {
    return NextResponse.json(
      { title: "Upstream unavailable", status: 502 },
      { status: 502 }
    );
  }

  if (upstream.status >= 300 && upstream.status < 400) {
    const location = upstream.headers.get("location");
    if (location) {
      return NextResponse.redirect(location, upstream.status);
    }
  }

  return new NextResponse(upstream.body, {
    status: upstream.status,
    headers: passthroughHeaders(upstream),
  });
}
