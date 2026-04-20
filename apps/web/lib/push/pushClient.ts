/**
 * Web Push client for EduConnect.
 *
 * Handles the three stages of browser push:
 *   1. Service worker must be registered at "/sw.js" (done in
 *      ServiceWorkerRegistrar at app load).
 *   2. User grants Notification permission.
 *   3. Browser produces a PushSubscription signed against the server's VAPID
 *      public key; we ship that subscription to the backend so it can push
 *      to this device/browser profile.
 *
 * All calls here are safe on the server (SSR) — they short-circuit when the
 * APIs aren't available.
 */

import { apiGet, apiPost, apiDelete, ApiError } from "@/lib/api-client";

export type PushSupport = {
  supported: boolean;
  permission: NotificationPermission | "unknown";
  /** True on iOS Safari before the user has added the app to Home Screen. */
  iosRequiresInstall: boolean;
};

export type VapidPublicKeyResponse = {
  publicKey: string | null;
  enabled: boolean;
};

const SUBSCRIBE_ENDPOINT = "/api/push/subscriptions";
const VAPID_ENDPOINT = "/api/push/vapid-public-key";

// ---------------------------------------------------------------------------
// Capability detection
// ---------------------------------------------------------------------------

export function detectPushSupport(): PushSupport {
  if (typeof window === "undefined") {
    return { supported: false, permission: "unknown", iosRequiresInstall: false };
  }

  const hasPushManager = "PushManager" in window;
  const hasServiceWorker = "serviceWorker" in navigator;
  const hasNotifications = "Notification" in window;

  const isIos = /iPad|iPhone|iPod/.test(navigator.userAgent);
  // iOS exposes Web Push only when the PWA is launched from Home Screen.
  // We detect "installed" via display-mode: standalone or navigator.standalone.
  const isStandalone =
    window.matchMedia?.("(display-mode: standalone)").matches ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true;

  const iosRequiresInstall = isIos && !isStandalone;

  return {
    supported: hasPushManager && hasServiceWorker && hasNotifications && !iosRequiresInstall,
    permission: hasNotifications ? Notification.permission : "unknown",
    iosRequiresInstall,
  };
}

// ---------------------------------------------------------------------------
// Subscription flow
// ---------------------------------------------------------------------------

/**
 * Ensure the user is subscribed to push. Idempotent: safe to call on every
 * login. Returns the subscription endpoint URL on success, or null if push
 * is not available / the user declined permission.
 */
export async function ensurePushSubscription(): Promise<string | null> {
  const support = detectPushSupport();
  if (!support.supported) return null;

  // Ask for permission only if we haven't already resolved this.
  if (Notification.permission === "default") {
    const result = await Notification.requestPermission();
    if (result !== "granted") return null;
  } else if (Notification.permission !== "granted") {
    return null;
  }

  const registration = await navigator.serviceWorker.ready;

  // If we already have a subscription, reuse it — browsers return the same
  // endpoint each time, so the server will upsert by endpoint.
  let subscription = await registration.pushManager.getSubscription();

  if (!subscription) {
    const vapid = await apiGet<VapidPublicKeyResponse>(VAPID_ENDPOINT).catch(() => null);
    if (!vapid?.enabled || !vapid.publicKey) {
      console.warn("[push] Server has Web Push disabled or no VAPID key configured.");
      return null;
    }

    subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      // Cast to BufferSource — TS lib.dom types flip-flop between
      // Uint8Array<ArrayBuffer> and ArrayBufferLike across versions.
      applicationServerKey: urlBase64ToUint8Array(vapid.publicKey) as BufferSource,
    });
  }

  await postSubscription(subscription);
  return subscription.endpoint;
}

/**
 * Remove the current device's subscription. Call on logout and on
 * "disable notifications" actions.
 */
export async function removePushSubscription(): Promise<void> {
  if (typeof window === "undefined" || !("serviceWorker" in navigator)) return;

  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  if (!subscription) return;

  try {
    await apiDelete<void>(
      `${SUBSCRIBE_ENDPOINT}?endpoint=${encodeURIComponent(subscription.endpoint)}`,
    );
  } catch (error) {
    // A 404 means the server already forgot it — that's fine on logout.
    if (!(error instanceof ApiError) || error.statusCode !== 404) {
      console.warn("[push] Failed to unregister subscription on server:", error);
    }
  }

  await subscription.unsubscribe().catch(() => {});
}

async function postSubscription(subscription: PushSubscription): Promise<void> {
  const json = subscription.toJSON();
  const keys = json.keys ?? {};

  await apiPost(SUBSCRIBE_ENDPOINT, {
    endpoint: subscription.endpoint,
    p256dh: keys.p256dh ?? "",
    auth: keys.auth ?? "",
    userAgent: navigator.userAgent,
  });
}

// ---------------------------------------------------------------------------
// VAPID key helpers
// ---------------------------------------------------------------------------

/**
 * Convert a base64url-encoded VAPID public key (as returned by the server)
 * into the Uint8Array form PushManager.subscribe expects.
 */
function urlBase64ToUint8Array(base64Url: string): Uint8Array {
  const padding = "=".repeat((4 - (base64Url.length % 4)) % 4);
  const base64 = (base64Url + padding).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(base64);
  const output = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i += 1) {
    output[i] = raw.charCodeAt(i);
  }
  return output;
}
