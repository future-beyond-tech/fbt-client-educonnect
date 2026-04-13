import { featureFlags } from "@/lib/feature-flags";

export type AnalyticsEventName =
  | "retention_step_complete"
  | "retention_card_view"
  | "empty_state_cta";

declare global {
  interface Window {
    gtag?: (...args: unknown[]) => void;
  }
}

export function trackEvent(
  name: AnalyticsEventName,
  params?: Record<string, string>
): void {
  if (typeof window === "undefined" || !featureFlags.analytics) {
    return;
  }
  if (typeof window.gtag === "function") {
    window.gtag("event", name, params ?? {});
  }
}
