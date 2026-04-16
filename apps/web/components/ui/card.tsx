import * as React from "react";
import { cn } from "@/lib/utils";

const Card = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref): React.ReactElement => (
  <div
    ref={ref}
    className={cn(
      "rounded-[28px] border border-border/70 bg-[linear-gradient(180deg,rgb(var(--card))_0%,rgb(var(--card))_55%,rgb(var(--muted))_160%)] text-card-foreground shadow-[0_30px_80px_-40px_rgba(15,40,69,0.22)] backdrop-blur-xl dark:bg-[linear-gradient(180deg,rgba(22,28,50,0.96)_0%,rgba(22,28,50,0.92)_55%,rgba(30,38,64,0.92)_160%)] dark:shadow-[0_34px_90px_-46px_rgba(10,14,24,0.82)]",
      className
    )}
    {...props}
  />
));
Card.displayName = "Card";

const CardHeader = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref): React.ReactElement => (
  <div
    ref={ref}
    className={cn("flex flex-col space-y-2 p-6", className)}
    {...props}
  />
));
CardHeader.displayName = "CardHeader";

const CardTitle = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLHeadingElement>
>(({ className, ...props }, ref): React.ReactElement => (
  <h2
    ref={ref}
    className={cn("text-xl font-semibold leading-tight text-foreground md:text-2xl", className)}
    {...props}
  />
));
CardTitle.displayName = "CardTitle";

const CardDescription = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLParagraphElement>
>(({ className, ...props }, ref): React.ReactElement => (
  <p
    ref={ref}
    className={cn("text-sm leading-6 text-muted-foreground", className)}
    {...props}
  />
));
CardDescription.displayName = "CardDescription";

const CardContent = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref): React.ReactElement => (
  <div
    ref={ref}
    className={cn("p-6 pt-0", className)}
    {...props}
  />
));
CardContent.displayName = "CardContent";

const CardFooter = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref): React.ReactElement => (
  <div
    ref={ref}
    className={cn("flex items-center p-6 pt-0", className)}
    {...props}
  />
));
CardFooter.displayName = "CardFooter";

export { Card, CardHeader, CardFooter, CardTitle, CardDescription, CardContent };
