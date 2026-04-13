/**
 * Feature flags (NEXT_PUBLIC_* are inlined at build time).
 * New UI ships behind flags until ready for general rollout.
 */
function envTrue(key: string): boolean {
  return process.env[key] === "true";
}

export const featureFlags = {
  retentionDashboardCard: envTrue("NEXT_PUBLIC_FEATURE_RETENTION_CARD"),
  analytics:
    envTrue("NEXT_PUBLIC_FEATURE_ANALYTICS") &&
    Boolean(process.env.NEXT_PUBLIC_ANALYTICS_MEASUREMENT_ID?.trim()),
} as const;
