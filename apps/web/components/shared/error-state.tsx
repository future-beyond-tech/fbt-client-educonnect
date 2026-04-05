import * as React from "react";
import { AlertCircle } from "lucide-react";
import { Button } from "@/components/ui/button";

export interface ErrorStateProps {
  title: string;
  message: string;
  onRetry?: () => void;
  icon?: React.ReactNode;
}

export function ErrorState({
  title,
  message,
  onRetry,
  icon,
}: ErrorStateProps): React.ReactElement {
  return (
    <div className="flex min-h-96 w-full flex-col items-center justify-center space-y-4 px-4 py-8">
      <div className="rounded-full bg-destructive/10 p-3">
        {icon || (
          <AlertCircle className="h-8 w-8 text-destructive" aria-hidden="true" />
        )}
      </div>
      <div className="space-y-2 text-center">
        <h2 className="text-xl font-semibold text-foreground">{title}</h2>
        <p className="text-sm text-muted-foreground">{message}</p>
      </div>
      {onRetry && (
        <Button onClick={onRetry} variant="outline" size="sm">
          Try Again
        </Button>
      )}
    </div>
  );
}
