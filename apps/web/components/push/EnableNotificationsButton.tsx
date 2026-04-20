"use client";

import { useCallback, useEffect, useState } from "react";
import { Bell, BellOff, Loader2 } from "lucide-react";
import {
  detectPushSupport,
  ensurePushSubscription,
  removePushSubscription,
} from "@/lib/push/pushClient";

type Status = "idle" | "subscribing" | "unsubscribing" | "subscribed" | "blocked" | "unsupported";

/**
 * Self-contained button that toggles push subscription for the current user.
 * Drop it anywhere inside an authenticated layout (e.g. profile/settings page,
 * dashboard header).
 *
 * Users will only see "Enable notifications" once; after that it flips to
 * "Disable" so they can turn push off without digging into browser settings.
 */
export function EnableNotificationsButton(): React.ReactElement | null {
  const [status, setStatus] = useState<Status>("idle");
  const [iosHint, setIosHint] = useState(false);

  useEffect(() => {
    const support = detectPushSupport();
    if (support.iosRequiresInstall) {
      setIosHint(true);
      setStatus("unsupported");
      return;
    }
    if (!support.supported) {
      setStatus("unsupported");
      return;
    }
    if (support.permission === "denied") {
      setStatus("blocked");
      return;
    }

    // Reflect current subscription state without prompting.
    navigator.serviceWorker.ready
      .then((reg) => reg.pushManager.getSubscription())
      .then((sub) => setStatus(sub ? "subscribed" : "idle"))
      .catch(() => setStatus("idle"));
  }, []);

  const handleEnable = useCallback(async () => {
    setStatus("subscribing");
    const endpoint = await ensurePushSubscription();
    setStatus(endpoint ? "subscribed" : "idle");
  }, []);

  const handleDisable = useCallback(async () => {
    setStatus("unsubscribing");
    await removePushSubscription();
    setStatus("idle");
  }, []);

  if (status === "unsupported") {
    return iosHint ? (
      <p className="text-sm text-muted-foreground">
        To enable notifications on iPhone: tap the Share button, then
        &quot;Add to Home Screen&quot; and open EduConnect from your home screen.
      </p>
    ) : null;
  }

  if (status === "blocked") {
    return (
      <p className="text-sm text-muted-foreground">
        Notifications are blocked in your browser settings. Open the site
        settings in your browser to re-enable them.
      </p>
    );
  }

  const isBusy = status === "subscribing" || status === "unsubscribing";
  const isSubscribed = status === "subscribed";

  return (
    <button
      type="button"
      onClick={isSubscribed ? handleDisable : handleEnable}
      disabled={isBusy}
      className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
    >
      {isBusy ? (
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
      ) : isSubscribed ? (
        <BellOff className="h-4 w-4" aria-hidden />
      ) : (
        <Bell className="h-4 w-4" aria-hidden />
      )}
      {isSubscribed ? "Disable notifications" : "Enable notifications"}
    </button>
  );
}
