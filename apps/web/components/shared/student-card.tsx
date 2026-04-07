"use client";

import * as React from "react";
import { Badge } from "@/components/ui/badge";

export interface StudentCardProps {
  name: string;
  rollNumber: string;
  className: string;
  section: string;
  isActive: boolean;
  showInactiveBadge?: boolean;
  onClick?: () => void;
}

export function StudentCard({
  name,
  rollNumber,
  className,
  section,
  isActive,
  showInactiveBadge = true,
  onClick,
}: StudentCardProps): React.ReactElement {
  const Wrapper = onClick ? "button" : "div";

  return (
    <Wrapper
      onClick={onClick}
      className={`flex w-full items-center justify-between rounded-lg border bg-card p-4 text-left transition-colors ${
        onClick
          ? "cursor-pointer hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          : ""
      } ${!isActive ? "opacity-60" : ""}`}
      aria-label={`Student ${name}, roll number ${rollNumber}`}
    >
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium text-foreground">{name}</p>
        <p className="text-sm text-muted-foreground">Roll: {rollNumber}</p>
      </div>
      <div className="ml-3 flex shrink-0 items-center gap-2">
        <Badge variant="secondary">
          {className}
          {section ? ` ${section}` : ""}
        </Badge>
        {showInactiveBadge && !isActive && (
          <Badge variant="destructive">Inactive</Badge>
        )}
      </div>
    </Wrapper>
  );
}
