"use client";

import * as React from "react";
import { animate, useReducedMotion } from "framer-motion";

export interface CountUpProps {
  /** The target value to animate to. */
  to: number;
  /** Starting value. Defaults to 0. */
  from?: number;
  /** Animation duration in seconds. */
  duration?: number;
  /** Decimal places to display. */
  decimals?: number;
  /** Optional formatter applied to the current value. */
  format?: (value: number) => string;
  className?: string;
}

/**
 * CountUp — animates a number from `from` to `to` on mount and when `to`
 * changes. Inspired by reactbits.dev's Count Up. Honors reduced motion.
 */
export function CountUp({
  to,
  from = 0,
  duration = 1.1,
  decimals = 0,
  format,
  className,
}: CountUpProps): React.ReactElement {
  const ref = React.useRef<HTMLSpanElement | null>(null);
  const reduced = useReducedMotion();

  React.useEffect(() => {
    const node = ref.current;
    if (!node) return;
    const render = (v: number): void => {
      node.textContent = format ? format(v) : v.toFixed(decimals);
    };
    if (reduced) {
      render(to);
      return;
    }
    const controls = animate(from, to, {
      duration,
      ease: [0.16, 1, 0.3, 1],
      onUpdate: render,
    });
    return (): void => controls.stop();
  }, [to, from, duration, decimals, format, reduced]);

  return (
    <span ref={ref} className={className}>
      {format ? format(from) : from.toFixed(decimals)}
    </span>
  );
}
