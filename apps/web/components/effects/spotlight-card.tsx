"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

export interface SpotlightCardProps extends React.HTMLAttributes<HTMLDivElement> {
  /** Base color of the spotlight (CSS color). Defaults to --primary. */
  spotlightColor?: string;
  /** Radius of the spotlight in pixels. */
  radius?: number;
}

/**
 * SpotlightCard — a card wrapper that paints a soft radial spotlight that
 * follows the mouse. Inspired by reactbits.dev's SpotlightCard. Works as a
 * drop-in wrapper around any content; falls back to a static glow on touch.
 */
export function SpotlightCard({
  children,
  className,
  spotlightColor,
  radius = 360,
  ...props
}: SpotlightCardProps): React.ReactElement {
  const ref = React.useRef<HTMLDivElement | null>(null);
  const [pos, setPos] = React.useState<{ x: number; y: number }>({
    x: 50,
    y: 20,
  });
  const [visible, setVisible] = React.useState(false);

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>): void => {
    const el = ref.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    setPos({
      x: ((e.clientX - rect.left) / rect.width) * 100,
      y: ((e.clientY - rect.top) / rect.height) * 100,
    });
  };

  const color = spotlightColor ?? "rgb(var(--primary) / 0.35)";

  return (
    <div
      ref={ref}
      onMouseMove={handleMouseMove}
      onMouseEnter={() => setVisible(true)}
      onMouseLeave={() => setVisible(false)}
      className={cn("relative overflow-hidden", className)}
      {...props}
    >
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 transition-opacity duration-500"
        style={{
          opacity: visible ? 1 : 0.55,
          background: `radial-gradient(${radius}px circle at ${pos.x}% ${pos.y}%, ${color}, transparent 55%)`,
        }}
      />
      <div className="relative">{children}</div>
    </div>
  );
}
