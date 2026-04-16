"use client";

import * as React from "react";
import { Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme } from "@/hooks/use-theme";
import { cn } from "@/lib/utils";

export interface ThemeToggleProps {
  className?: string;
}

export function ThemeToggle({
  className,
}: ThemeToggleProps): React.ReactElement {
  const { theme, isHydrated, toggleTheme } = useTheme();
  const nextTheme = theme === "dark" ? "light" : "dark";
  const label = isHydrated
    ? `Switch to ${nextTheme} theme`
    : "Toggle theme";

  return (
    <Button
      type="button"
      variant="outline"
      size="icon"
      onClick={toggleTheme}
      aria-label={label}
      title={label}
      className={cn(
        "h-11 w-11 rounded-full border-border/70 bg-card/82 shadow-[0_18px_38px_-28px_rgba(15,40,69,0.38)] backdrop-blur-xl dark:bg-card/92",
        className
      )}
    >
      {theme === "dark" ? (
        <Sun className="h-5 w-5 text-cyber-amber" aria-hidden="true" />
      ) : (
        <Moon className="h-5 w-5 text-primary" aria-hidden="true" />
      )}
    </Button>
  );
}
