const ATTACHMENT_DOWNLOAD_PATH_PATTERN =
  /^\/api\/attachments\/[0-9a-fA-F-]{36}\/download$/;

function configuredApiOrigin(): string | null {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL;
  if (!apiUrl) return null;

  try {
    return new URL(apiUrl).origin;
  } catch {
    return null;
  }
}

function parseAbsoluteUrl(value: string): URL | null {
  try {
    return new URL(value);
  } catch {
    return null;
  }
}

function isAttachmentDownloadPath(pathname: string): boolean {
  return ATTACHMENT_DOWNLOAD_PATH_PATTERN.test(pathname);
}

function buildRelativeUrl(pathname: string, search: string): string {
  return search ? `${pathname}${search}` : pathname;
}

function isRelativeAttachmentProxyUrl(value: string): boolean {
  if (!value.startsWith("/")) return false;

  try {
    const parsed = new URL(value, "https://educonnect.local");
    return isAttachmentDownloadPath(parsed.pathname);
  } catch {
    return false;
  }
}

/**
 * Notice-attachment downloads must stay on the web origin so the browser can
 * send the refresh cookie to the Next.js proxy route. Some environments still
 * surface absolute backend URLs, so we normalize them here before rendering.
 */
export function normalizeAttachmentViewUrl(rawUrl: string): string {
  if (!rawUrl) return rawUrl;
  if (isRelativeAttachmentProxyUrl(rawUrl)) return rawUrl;

  const apiOrigin = configuredApiOrigin();
  const parsed = parseAbsoluteUrl(rawUrl);
  if (!apiOrigin || !parsed) return rawUrl;

  if (parsed.origin !== apiOrigin || !isAttachmentDownloadPath(parsed.pathname)) {
    return rawUrl;
  }

  return buildRelativeUrl(parsed.pathname, parsed.search);
}

/**
 * Adds an explicit forced-download mode for same-origin attachment proxy URLs.
 * Direct storage URLs are returned untouched because mutating their query string
 * would invalidate the presigned signature.
 */
export function buildAttachmentDownloadUrl(rawUrl: string): string {
  const viewUrl = normalizeAttachmentViewUrl(rawUrl);
  if (!isRelativeAttachmentProxyUrl(viewUrl)) {
    return viewUrl;
  }

  const parsed = new URL(viewUrl, "https://educonnect.local");
  parsed.searchParams.set("download", "true");
  return buildRelativeUrl(parsed.pathname, parsed.search);
}
