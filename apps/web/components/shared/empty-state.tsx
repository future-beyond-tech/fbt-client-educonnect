"use client";

import * as React from "react";
import { Inbox } from "lucide-react";
import { Button } from "@/components/ui/button";
import { trackEvent } from "@/lib/analytics";
import { SpotlightCard } from "@/components/effects/spotlight-card";

function DefaultEmptyIllustration(): React.ReactElement {
  return (
    <svg
      viewBox="0 0 120 120"
      className="pointer-events-none h-28 w-28 text-cyber-cyan/40"
      aria-hidden="true"
      focusable="false"
    >
      <rect
        x="8"
        y="8"
        width="104"
        height="104"
        rx="12"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path
        d="M24 40h72M24 60h72M24 80h52M40 24v72M60 24v72M80 24v72"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        opacity="0.85"
      />
      <circle cx="86" cy="86" r="10" className="fill-cyber-amber/40" />
    </svg>
  );
}

export interface EmptyStateProps {
  title: string;
  description: string;
  icon?: React.ReactNode;
  action?: {
    label: string;
    onClick: () => void;
  };
}

export function EmptyState({
  title,
  description,
  icon,
  action,
}: EmptyStateProps): React.ReactElement {
  const handleAction = (): void => {
    trackEvent("empty_state_cta", { title });
    action?.onClick();
  };

  return (
    <SpotlightCard
      radius={520}
      spotlightColor="rgb(var(--accent) / 0.25)"
      className="w-full rounded-[30px] border border-dashed border-border/80 bg-card/55 shadow-[0_24px_64px_-42px_rgba(15,40,69,0.35)] backdrop-blur-sm"
    >
      <div className="flex min-h-96 w-full flex-col items-center justify-center space-y-5 px-5 py-10 text-center">
      <div className="relative flex flex-col items-center gap-3">
        <DefaultEmptyIllustration />
        <div className="rounded-full border border-border/70 bg-card/82 p-4 shadow-[0_18px_40px_-28px_rgba(15,40,69,0.4)] backdrop-blur-sm dark:bg-card/92">
          {icon ?? (
            <Inbox className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
          )}
        </div>
      </div>
      <div className="space-y-2">
        <h2 className="text-2xl font-semibold text-foreground">{title}</h2>
        <p className="max-w-lg text-sm leading-7 text-muted-foreground">{description}</p>
      </div>
      {action ? (
        <Button
          onClick={handleAction}
          size="sm"
          variant="premium"
          className="min-h-touchTarget"
        >
          {action.label}
        </Button>
      ) : null}
      </div>
    </SpotlightCard>
  );
}
