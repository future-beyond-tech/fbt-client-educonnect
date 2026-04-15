"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

export interface StarBorderProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Speed of the rotating light, in seconds. Default 6s. */
  speed?: number;
  /** Thickness of the animated border in px. Default 1.5. */
  thickness?: number;
}

/**
 * StarBorder — a button with an animated conic-gradient border that sweeps
 * around the element, inspired by reactbits.dev's StarBorder. Keeps all
 * native button behavior; apply your own typography/padding via className.
 */
export const StarBorder = React.forwardRef<HTMLButtonElement, StarBorderProps>(
  (
    { className, children, speed = 6, thickness = 1.5, style, ...props },
    ref
  ): React.ReactElement => {
    return (
      <button
        ref={ref}
        className={cn(
          "focus-ring relative inline-flex items-center justify-center gap-2 overflow-hidden rounded-full font-semibold text-primary-foreground transition-transform duration-200 hover:-translate-y-0.5 active:translate-y-0 disabled:pointer-events-none disabled:opacity-50",
          className
        )}
        style={
          {
            padding: thickness,
            ...style,
          } as React.CSSProperties
        }
        {...props}
      >
        <span
          aria-hidden="true"
          className="absolute inset-0 rounded-full"
          style={{
            background: `conic-gradient(from 0deg, rgb(var(--primary)), rgb(var(--accent)), rgb(var(--glow-3)), rgb(var(--primary)))`,
            animation: `star-border-spin ${speed}s linear infinite`,
          }}
        />
        <span className="relative inline-flex h-full w-full items-center justify-center gap-2 rounded-full bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--accent)))] px-5 py-2.5 shadow-[0_18px_44px_-22px_rgba(76,29,149,0.7)]">
          {children}
        </span>
        <style>{`
          @keyframes star-border-spin { to { transform: rotate(1turn); } }
          @media (prefers-reduced-motion: reduce) {
            button > span[aria-hidden] { animation: none !important; }
          }
        `}</style>
      </button>
    );
  }
);
StarBorder.displayName = "StarBorder";
