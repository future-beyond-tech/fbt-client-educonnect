import * as React from "react";
import { Inbox } from "lucide-react";
import { Button } from "@/components/ui/button";

export interface EmptyStateProps {
  title: string;
  description: string;
  icon?: React.ReactNode;
  action?: {
    label: string;
    onClick: () => void;
  };
}

export function EmptyState({
  title,
  description,
  icon,
  action,
}: EmptyStateProps): React.ReactElement {
  return (
    <div className="flex min-h-96 w-full flex-col items-center justify-center space-y-4 px-4 py-8">
      <div className="rounded-full bg-muted p-3">
        {icon || (
          <Inbox className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
        )}
      </div>
      <div className="space-y-2 text-center">
        <h2 className="text-xl font-semibold text-foreground">{title}</h2>
        <p className="text-sm text-muted-foreground">{description}</p>
      </div>
      {action && (
        <Button onClick={action.onClick} size="sm">
          {action.label}
        </Button>
      )}
    </div>
  );
}
