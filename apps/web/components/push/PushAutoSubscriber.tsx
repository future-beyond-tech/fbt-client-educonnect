"use client";

import { useEffect } from "react";
import { detectPushSupport, ensurePushSubscription } from "@/lib/push/pushClient";

/**
 * Silently ensures the signed-in user's current device is subscribed to push.
 *
 * Mount inside the authenticated layout (e.g. apps/web/app/(dashboard)/layout.tsx)
 * so it runs once per session and re-registers subscriptions that were
 * invalidated by browser cleanups or VAPID rotation.
 *
 * Intentionally does NOT prompt the user for permission — it only attaches to
 * an already-granted permission. Use EnableNotificationsButton for the
 * first-time opt-in flow.
 */
export function PushAutoSubscriber(): null {
  useEffect(() => {
    const support = detectPushSupport();
    if (!support.supported || support.permission !== "granted") {
      return;
    }

    ensurePushSubscription().catch((error) => {
      console.warn("[push] auto-subscribe failed:", error);
    });

    function handleMessage(event: MessageEvent) {
      if (event.data?.type === "pushsubscriptionchange") {
        ensurePushSubscription().catch(() => {});
      }
    }

    if ("serviceWorker" in navigator) {
      navigator.serviceWorker.addEventListener("message", handleMessage);
      return () => {
        navigator.serviceWorker.removeEventListener("message", handleMessage);
      };
    }

    return undefined;
  }, []);

  return null;
}
