import * as React from "react";
import { cn } from "@/lib/utils";

export interface SkeletonProps extends React.HTMLAttributes<HTMLDivElement> {}

function Skeleton({ className, ...props }: SkeletonProps): React.ReactElement {
  return (
    <div
      className={cn(
        "rounded-[20px] bg-[linear-gradient(110deg,rgba(226,232,240,0.55),rgba(255,255,255,0.95),rgba(226,232,240,0.55))] bg-[length:200%_100%] animate-[shimmer_1.6s_ease-in-out_infinite] dark:bg-[linear-gradient(110deg,rgba(20,42,63,0.9),rgba(38,68,94,0.96),rgba(20,42,63,0.9))]",
        className
      )}
      {...props}
    />
  );
}

export { Skeleton };
