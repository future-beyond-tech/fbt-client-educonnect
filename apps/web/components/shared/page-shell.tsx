import * as React from "react";
import { cn } from "@/lib/utils";

export interface PageShellProps extends React.HTMLAttributes<HTMLDivElement> {}

export function PageShell({
  className,
  children,
  ...props
}: PageShellProps): React.ReactElement {
  return (
    <div
      className={cn(
        "mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 pb-24 pt-6 md:px-8 md:pb-10 md:pt-8",
        className
      )}
      {...props}
    >
      {children}
    </div>
  );
}

interface PageStat {
  label: string;
  value: string;
}

export interface PageHeaderProps {
  eyebrow?: string;
  title: string;
  description: string;
  icon?: React.ReactNode;
  actions?: React.ReactNode;
  backAction?: React.ReactNode;
  stats?: PageStat[];
  className?: string;
}

export function PageHeader({
  eyebrow,
  title,
  description,
  icon,
  actions,
  backAction,
  stats,
  className,
}: PageHeaderProps): React.ReactElement {
  return (
    <section
      className={cn(
        "relative overflow-hidden rounded-[32px] border border-border/70 bg-[linear-gradient(145deg,rgba(255,255,255,0.96),rgba(240,246,250,0.86))] p-5 shadow-[0_30px_80px_-38px_rgba(15,23,42,0.48)] backdrop-blur-xl [animation:enter-up_420ms_ease-out_both] dark:bg-[linear-gradient(145deg,rgba(12,30,48,0.96),rgba(8,18,31,0.92))] dark:shadow-[0_34px_90px_-44px_rgba(2,12,24,0.84)] md:p-7",
        className
      )}
    >
      <div className="pointer-events-none absolute -right-12 -top-16 h-44 w-44 rounded-full bg-[radial-gradient(circle,rgba(58,199,179,0.18),transparent_65%)] [animation:float_8s_ease-in-out_infinite]" />
      <div className="pointer-events-none absolute -bottom-16 left-8 h-40 w-40 rounded-full bg-[radial-gradient(circle,rgba(255,176,32,0.16),transparent_65%)]" />
      <div className="relative flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 space-y-4">
          {(backAction || eyebrow) && (
            <div className="flex flex-wrap items-center gap-3">
              {backAction}
              {eyebrow ? (
                <span className="inline-flex rounded-full border border-primary/10 bg-primary/8 px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-primary/80">
                  {eyebrow}
                </span>
              ) : null}
            </div>
          )}
          <div className="flex items-start gap-4">
            {icon ? (
              <div className="hidden h-14 w-14 items-center justify-center rounded-[22px] bg-[linear-gradient(135deg,rgba(22,88,136,0.12),rgba(58,199,179,0.16))] text-primary shadow-inner shadow-white/70 dark:shadow-black/30 sm:flex">
                {icon}
              </div>
            ) : null}
            <div className="min-w-0 space-y-3">
              <h1 className="text-3xl font-semibold text-foreground md:text-4xl">
                {title}
              </h1>
              <p className="max-w-3xl text-sm leading-7 text-muted-foreground md:text-base">
                {description}
              </p>
            </div>
          </div>
          {stats && stats.length > 0 ? (
            <div className="flex flex-wrap gap-3">
              {stats.map((stat) => (
                <div
                  key={`${stat.label}-${stat.value}`}
                  className="rounded-[22px] border border-border/70 bg-card/74 px-4 py-3 shadow-[0_16px_36px_-28px_rgba(15,23,42,0.45)] backdrop-blur-sm dark:bg-card/86"
                >
                  <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-muted-foreground">
                    {stat.label}
                  </p>
                  <p className="mt-1 text-lg font-semibold text-foreground">
                    {stat.value}
                  </p>
                </div>
              ))}
            </div>
          ) : null}
        </div>
        {actions ? (
          <div className="flex shrink-0 flex-wrap items-center gap-3">
            {actions}
          </div>
        ) : null}
      </div>
    </section>
  );
}

export interface PageSectionProps
  extends React.HTMLAttributes<HTMLDivElement> {}

export function PageSection({
  className,
  children,
  ...props
}: PageSectionProps): React.ReactElement {
  return (
    <section
      className={cn(
        "rounded-[28px] border border-border/70 bg-card/88 p-4 shadow-[0_28px_70px_-40px_rgba(15,23,42,0.45)] backdrop-blur-xl dark:bg-card/92 dark:shadow-[0_32px_80px_-42px_rgba(2,12,24,0.84)] md:p-6",
        className
      )}
      {...props}
    >
      {children}
    </section>
  );
}
