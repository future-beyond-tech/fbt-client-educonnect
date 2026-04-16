import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const buttonVariants = cva(
  "focus-ring inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-full text-sm font-semibold tracking-[-0.01em] ring-offset-background transition-all duration-200 disabled:pointer-events-none disabled:opacity-50 disabled:shadow-none [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--primary-strong)))] text-primary-foreground shadow-[0_18px_44px_-22px_rgba(15,40,69,0.55)] hover:-translate-y-0.5 hover:shadow-[0_26px_56px_-24px_rgba(15,40,69,0.6)] active:translate-y-0 active:shadow-[0_16px_34px_-20px_rgba(15,40,69,0.5)]",
        premium:
          "btn-premium text-primary-foreground shadow-[0_22px_50px_-24px_rgba(15,40,69,0.55)] hover:-translate-y-0.5 active:translate-y-0",
        destructive:
          "bg-destructive text-destructive-foreground shadow-[0_18px_36px_-22px_rgba(220,38,38,0.55)] hover:-translate-y-0.5 hover:bg-destructive/90 active:translate-y-0",
        outline:
          "border border-border/80 bg-card/80 text-foreground shadow-[0_14px_32px_-28px_rgba(120,90,60,0.32)] backdrop-blur-sm hover:-translate-y-0.5 hover:border-primary/30 hover:bg-card/95 active:translate-y-0",
        secondary:
          "bg-secondary text-secondary-foreground shadow-[0_14px_34px_-26px_rgba(15,40,69,0.22)] hover:-translate-y-0.5 hover:bg-secondary/90 active:translate-y-0",
        ghost:
          "bg-transparent text-muted-foreground hover:bg-card/70 hover:text-foreground active:bg-card/90",
        link: "rounded-none px-0 text-primary shadow-none underline-offset-4 hover:underline active:text-primary/80",
      },
      size: {
        default: "h-12 px-5",
        sm: "h-10 px-4 text-sm",
        lg: "h-14 px-7 text-base",
        icon: "h-11 w-11",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    { className, variant, size, asChild: _asChild, ...props },
    ref
  ): React.ReactElement => (
    <button
      className={cn(buttonVariants({ variant, size, className }))}
      ref={ref}
      {...props}
    />
  )
);
Button.displayName = "Button";

export { Button, buttonVariants };
