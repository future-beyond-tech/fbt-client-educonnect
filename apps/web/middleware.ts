import { NextResponse, type NextRequest } from "next/server";

// Pull origins out of env at edge runtime. Empty values are filtered so CSP
// stays strict in environments where a given dependency isn't configured.
function originOrEmpty(url: string | undefined): string {
  if (!url) return "";
  try {
    return new URL(url).origin;
  } catch {
    return "";
  }
}

function originsFromCsv(value: string | undefined): string[] {
  if (!value) return [];

  return value
    .split(",")
    .map((entry) => originOrEmpty(entry.trim()))
    .filter(Boolean);
}

function buildCsp(nonce: string): string {
  const isProd = process.env.NODE_ENV === "production";

  const apiOrigin = originOrEmpty(process.env.NEXT_PUBLIC_API_URL);
  const mediaOrigin = originOrEmpty(process.env.NEXT_PUBLIC_MEDIA_BASE_URL);
  const uploadOrigins = Array.from(
    new Set([
      originOrEmpty(process.env.S3_SERVICE_URL),
      ...originsFromCsv(process.env.NEXT_PUBLIC_ATTACHMENT_UPLOAD_ORIGINS),
    ].filter(Boolean))
  );
  const sentryEnabled = Boolean(process.env.NEXT_PUBLIC_SENTRY_DSN?.trim());

  const connectSrc = Array.from(
    new Set([
      "'self'",
      apiOrigin,
      // Browser uploads go directly to storage via presigned URLs, so CSP
      // must allow the storage origin in addition to the API origin.
      ...uploadOrigins,
      ...(sentryEnabled
        ? ["https://*.sentry.io", "https://*.ingest.sentry.io"]
        : []),
    ].filter(Boolean))
  ).join(" ");

  const imgSrc = ["'self'", "data:", "blob:", mediaOrigin]
    .filter(Boolean)
    .join(" ");

  // Only emit frame-src when an explicit media origin is configured.
  // Empty → omit the directive entirely so it falls back to default-src
  // 'self' (existing behaviour). Admin notice-preview embeds PDFs in
  // <iframe>; the audit-redirect endpoint 302s to the R2 media origin,
  // so the embeddable target is mediaOrigin, not 'self'.
  const frameSrc = mediaOrigin ? `'self' ${mediaOrigin}` : "";

  // 'strict-dynamic' lets nonced root scripts transitively load Next.js bundles
  // without needing each chunk hash listed. 'self' is ignored when strict-dynamic
  // is present but keeping it satisfies non-nonce-aware scanners.
  // Dev needs 'unsafe-eval' for React Refresh; prod stays strict.
  const scriptSrc = [
    "'self'",
    `'nonce-${nonce}'`,
    "'strict-dynamic'",
    isProd ? "" : "'unsafe-eval'",
  ]
    .filter(Boolean)
    .join(" ");

  const directives = [
    `default-src 'self'`,
    `script-src ${scriptSrc}`,
    `style-src 'self' 'unsafe-inline'`,
    `img-src ${imgSrc}`,
    `font-src 'self' data:`,
    `connect-src ${connectSrc}`,
    `worker-src 'self' blob:`,
    frameSrc ? `frame-src ${frameSrc}` : "",
    `frame-ancestors 'none'`,
    `base-uri 'self'`,
    `form-action 'self'`,
    `object-src 'none'`,
    isProd ? `upgrade-insecure-requests` : "",
  ]
    .filter(Boolean)
    .join("; ");

  return directives;
}

function generateNonce(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

export function middleware(request: NextRequest): NextResponse {
  const nonce = generateNonce();
  const csp = buildCsp(nonce);

  // Forward the nonce into the request so Server Components can read it via
  // next/headers and attach it to inline <script> tags.
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("content-security-policy", csp);

  const response = NextResponse.next({
    request: { headers: requestHeaders },
  });

  response.headers.set("Content-Security-Policy", csp);
  return response;
}

// Exclude static assets and PWA files so we don't pay the middleware cost on
// every icon/font fetch and don't accidentally CSP the service worker script.
export const config = {
  matcher: [
    {
      source:
        "/((?!api|_next/static|_next/image|favicon.ico|manifest.json|sw.js|workbox-.*\\.js|icon-.*\\.png|apple-touch-icon\\.png).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};
