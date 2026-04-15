"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

export interface AuroraProps extends React.HTMLAttributes<HTMLDivElement> {
  /** Intensity of the aurora bloom, 0–1. */
  intensity?: number;
}

/**
 * Aurora — a CSS-only animated gradient backdrop inspired by reactbits.dev.
 * Renders three slowly-drifting radial blooms that read as an aurora sheen
 * behind whatever children you place on top. Honors prefers-reduced-motion
 * (animation disabled but gradient still visible).
 */
export function Aurora({
  intensity = 1,
  className,
  style,
  ...props
}: AuroraProps): React.ReactElement {
  const a = Math.max(0, Math.min(1, intensity));
  return (
    <div
      aria-hidden="true"
      className={cn(
        "pointer-events-none absolute inset-0 overflow-hidden",
        className
      )}
      style={style}
      {...props}
    >
      <div
        className="absolute -inset-[20%] opacity-90 mix-blend-screen dark:mix-blend-plus-lighter [animation:aurora-pan_22s_ease-in-out_infinite_alternate]"
        style={{
          backgroundImage: [
            `radial-gradient(40% 50% at 20% 30%, rgb(var(--glow-1) / ${0.55 * a}), transparent 70%)`,
            `radial-gradient(35% 45% at 80% 25%, rgb(var(--glow-2) / ${0.5 * a}), transparent 72%)`,
            `radial-gradient(45% 50% at 60% 80%, rgb(var(--glow-3) / ${0.5 * a}), transparent 70%)`,
            `radial-gradient(30% 40% at 10% 80%, rgb(var(--primary) / ${0.35 * a}), transparent 70%)`,
          ].join(", "),
          filter: "blur(40px) saturate(125%)",
        }}
      />
      <div
        className="absolute inset-0 opacity-70 [animation:aurora-shimmer_14s_ease-in-out_infinite]"
        style={{
          backgroundImage:
            "linear-gradient(115deg, transparent 40%, rgb(var(--accent) / 0.10) 50%, transparent 60%)",
        }}
      />
      <style>{`
        @keyframes aurora-pan {
          0% { transform: translate3d(-4%, -2%, 0) rotate(0deg); }
          50% { transform: translate3d(3%, 2%, 0) rotate(8deg); }
          100% { transform: translate3d(-2%, 3%, 0) rotate(-6deg); }
        }
        @keyframes aurora-shimmer {
          0%, 100% { background-position: 0% 50%; }
          50% { background-position: 100% 50%; }
        }
        @media (prefers-reduced-motion: reduce) {
          [style*="aurora-pan"], [style*="aurora-shimmer"] { animation: none !important; }
        }
      `}</style>
    </div>
  );
}
