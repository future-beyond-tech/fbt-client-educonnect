"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { motion, useReducedMotion } from "framer-motion";
import { CheckCircle2, Circle } from "lucide-react";
import type { RoleType } from "@/lib/constants";
import { retentionStepsByRole } from "@/lib/constants";
import {
  loadRetentionCompleted,
  saveRetentionCompleted,
} from "@/lib/retention-storage";
import { trackEvent } from "@/lib/analytics";
import { cn } from "@/lib/utils";

interface RetentionProgressCardProps {
  role: RoleType;
}

export function RetentionProgressCard({
  role,
}: RetentionProgressCardProps): React.ReactElement {
  const pathname = usePathname();
  const steps = retentionStepsByRole[role];
  const reduceMotion = useReducedMotion();
  const [completed, setCompleted] = React.useState<Set<string>>(() =>
    loadRetentionCompleted(role)
  );
  const viewedRef = React.useRef(false);

  React.useEffect(() => {
    setCompleted(loadRetentionCompleted(role));
  }, [role]);

  React.useEffect(() => {
    if (!viewedRef.current) {
      viewedRef.current = true;
      trackEvent("retention_card_view", { role });
    }
  }, [role]);

  React.useEffect(() => {
    setCompleted((prev) => {
      const next = new Set(prev);
      let changed = false;
      for (const step of steps) {
        const match =
          pathname === step.href || pathname.startsWith(`${step.href}/`);
        if (match && !next.has(step.id)) {
          next.add(step.id);
          changed = true;
          trackEvent("retention_step_complete", {
            step_id: step.id,
            role,
          });
        }
      }
      if (changed) {
        saveRetentionCompleted(role, next);
        return next;
      }
      return prev;
    });
  }, [pathname, role, steps]);

  const total = steps.length;
  const done = steps.filter((s) => completed.has(s.id)).length;
  const percent = total === 0 ? 100 : Math.round((done / total) * 100);
  const nextStep = steps.find((s) => !completed.has(s.id));

  return (
    <motion.section
      initial={reduceMotion ? false : { opacity: 0, y: 6 }}
      animate={reduceMotion ? undefined : { opacity: 1, y: 0 }}
      transition={{ duration: 0.22 }}
      className="px-4 pt-4 md:pl-[19rem] md:pr-6"
      aria-labelledby="retention-heading"
    >
      <div className="mx-auto max-w-7xl rounded-[28px] border border-border/70 bg-card/75 px-4 py-4 shadow-[0_24px_70px_-42px_rgba(15,23,42,0.46)] backdrop-blur-xl dark:bg-card/88 dark:shadow-[0_28px_78px_-46px_rgba(2,12,24,0.84)] md:px-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2
            id="retention-heading"
            className="text-sm font-semibold text-foreground"
          >
            This week&apos;s focus
          </h2>
          <p
            className="text-xs tabular-nums text-muted-foreground"
            aria-live="polite"
            aria-atomic="true"
          >
            {percent}% complete
          </p>
        </div>
        <div
          className="h-2 overflow-hidden rounded-full bg-muted"
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={percent}
          aria-label="Weekly focus progress"
        >
          <div
            className="h-full rounded-full bg-primary transition-all duration-300 ease-out"
            style={{ width: `${percent}%` }}
          />
        </div>
        <ul className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {steps.map((step) => {
            const isDone = completed.has(step.id);
            return (
              <li key={step.id}>
                <Link
                  href={step.href}
                  className={cn(
                    "flex min-h-touchTarget items-center gap-2 rounded-[20px] border border-transparent px-3 py-3 text-sm transition-colors",
                    "hover:border-border hover:bg-card/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background",
                    isDone && "text-muted-foreground"
                  )}
                >
                  {isDone ? (
                    <CheckCircle2
                      className="size-5 shrink-0 text-cyber-cyan"
                      aria-hidden="true"
                    />
                  ) : (
                    <Circle
                      className="size-5 shrink-0 text-muted-foreground"
                      aria-hidden="true"
                    />
                  )}
                  <span
                    className={cn(
                      isDone &&
                        "line-through decoration-muted-foreground/60"
                    )}
                  >
                    {step.label}
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
        {nextStep !== undefined && percent < 100 ? (
          <p className="text-xs text-muted-foreground">
            Next:{" "}
            <Link
              href={nextStep.href}
              className="font-medium text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {nextStep.label}
            </Link>
          </p>
        ) : null}
        {percent === 100 ? (
          <p className="text-sm font-medium text-cyber-cyan" role="status">
            All caught up — great work.
          </p>
        ) : null}
      </div>
    </motion.section>
  );
}
