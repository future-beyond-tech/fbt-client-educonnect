"use client";

import * as React from "react";
import { motion, useReducedMotion } from "framer-motion";
import { cn } from "@/lib/utils";

export interface SplitTextProps {
  text: string;
  className?: string;
  /** Delay between characters in seconds. Default 0.035s. */
  stagger?: number;
  /** Overall animation duration per character in seconds. */
  duration?: number;
  /** Render as this element. Default "span". */
  as?: "span" | "h1" | "h2" | "h3" | "p";
}

/**
 * SplitText — reveals each character with a staggered blur + slide-up,
 * inspired by reactbits.dev's SplitText/BlurText. Falls back to plain
 * text when the user prefers reduced motion.
 */
export function SplitText({
  text,
  className,
  stagger = 0.035,
  duration = 0.55,
  as = "span",
}: SplitTextProps): React.ReactElement {
  const reduced = useReducedMotion();
  const MotionTag = motion[as] as typeof motion.span;

  if (reduced) {
    const Tag = as as React.ElementType;
    return <Tag className={className}>{text}</Tag>;
  }

  const chars = Array.from(text);
  return (
    <MotionTag
      className={cn("inline-block", className)}
      initial="hidden"
      animate="visible"
      variants={{ visible: { transition: { staggerChildren: stagger } } }}
      aria-label={text}
    >
      {chars.map((ch, i) => (
        <motion.span
          key={`${ch}-${i}`}
          className="inline-block whitespace-pre"
          variants={{
            hidden: { opacity: 0, y: "0.4em", filter: "blur(8px)" },
            visible: {
              opacity: 1,
              y: 0,
              filter: "blur(0px)",
              transition: { duration, ease: [0.16, 1, 0.3, 1] },
            },
          }}
        >
          {ch === " " ? "\u00A0" : ch}
        </motion.span>
      ))}
    </MotionTag>
  );
}
