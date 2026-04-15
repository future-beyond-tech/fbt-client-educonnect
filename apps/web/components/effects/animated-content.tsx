"use client";

import * as React from "react";
import { motion, useReducedMotion } from "framer-motion";
import { usePathname } from "next/navigation";

export interface AnimatedContentProps {
  children: React.ReactNode;
  /** Animation distance in pixels on the Y axis. Default 18. */
  distance?: number;
  /** Duration in seconds. Default 0.5. */
  duration?: number;
  /** Optional key override. Defaults to the current pathname. */
  keyOverride?: string;
  className?: string;
}

/**
 * AnimatedContent — fades and slides children into view on mount and on
 * route changes (keyed by pathname). Inspired by reactbits.dev's
 * AnimatedContent. Honors reduced motion.
 */
export function AnimatedContent({
  children,
  distance = 18,
  duration = 0.5,
  keyOverride,
  className,
}: AnimatedContentProps): React.ReactElement {
  const reduced = useReducedMotion();
  const pathname = usePathname();
  const motionKey = keyOverride ?? pathname ?? "content";

  if (reduced) {
    return <div className={className}>{children}</div>;
  }

  return (
    <motion.div
      key={motionKey}
      className={className}
      initial={{ opacity: 0, y: distance }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration, ease: [0.16, 1, 0.3, 1] }}
    >
      {children}
    </motion.div>
  );
}
