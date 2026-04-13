import * as React from "react";
import {
  CheckCircle2,
  CircleAlert,
  Info,
  TriangleAlert,
} from "lucide-react";
import { cn } from "@/lib/utils";

type BannerVariant = "success" | "error" | "info" | "warning";

const variantMap: Record<
  BannerVariant,
  {
    icon: React.ComponentType<{ className?: string }>;
    className: string;
    iconClassName: string;
  }
> = {
  success: {
    icon: CheckCircle2,
    className:
      "border-emerald-200/80 bg-emerald-50/90 text-emerald-950 dark:border-emerald-400/20 dark:bg-emerald-500/10 dark:text-emerald-100",
    iconClassName: "text-emerald-600 dark:text-emerald-300",
  },
  error: {
    icon: CircleAlert,
    className:
      "border-destructive/20 bg-destructive/10 text-destructive dark:border-destructive/25 dark:bg-destructive/12 dark:text-destructive-foreground",
    iconClassName: "text-destructive",
  },
  info: {
    icon: Info,
    className:
      "border-primary/12 bg-primary/8 text-foreground dark:border-primary/18 dark:bg-primary/10",
    iconClassName: "text-primary",
  },
  warning: {
    icon: TriangleAlert,
    className:
      "border-amber-200/80 bg-amber-50/90 text-amber-950 dark:border-amber-300/20 dark:bg-amber-400/10 dark:text-amber-100",
    iconClassName: "text-amber-600 dark:text-amber-300",
  },
};

export interface StatusBannerProps
  extends React.HTMLAttributes<HTMLDivElement> {
  variant?: BannerVariant;
  title?: string;
}

export function StatusBanner({
  variant = "info",
  title,
  className,
  children,
  ...props
}: StatusBannerProps): React.ReactElement {
  const config = variantMap[variant];
  const Icon = config.icon;

  return (
    <div
      className={cn(
        "flex items-start gap-3 rounded-[22px] border px-4 py-4 shadow-[0_20px_44px_-34px_rgba(15,23,42,0.36)] backdrop-blur-sm",
        config.className,
        className
      )}
      {...props}
    >
      <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-white/65 dark:bg-black/10">
        <Icon className={cn("h-4 w-4", config.iconClassName)} aria-hidden="true" />
      </div>
      <div className="min-w-0 space-y-1">
        {title ? (
          <p className="text-sm font-semibold tracking-tight">{title}</p>
        ) : null}
        <div className="text-sm leading-6">{children}</div>
      </div>
    </div>
  );
}
