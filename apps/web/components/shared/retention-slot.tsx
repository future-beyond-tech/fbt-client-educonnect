"use client";

import * as React from "react";
import { useAuth } from "@/hooks/use-auth";
import { featureFlags } from "@/lib/feature-flags";
import { RetentionProgressCard } from "@/components/shared/retention-progress-card";

export function RetentionSlot(): React.ReactElement | null {
  const { user } = useAuth();
  if (!featureFlags.retentionDashboardCard || !user) {
    return null;
  }
  return <RetentionProgressCard role={user.role} />;
}
