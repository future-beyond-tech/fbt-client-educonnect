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
    <div className="flex min-h-96 w-full flex-col items-center justify-center space-y-5 rounded-[30px] border border-destructive/15 bg-card/62 px-5 py-10 text-center shadow-[0_24px_64px_-42px_rgba(15,23,42,0.35)] backdrop-blur-sm">
      <div className="rounded-full border border-destructive/20 bg-destructive/10 p-4">
        {icon || (
          <AlertCircle className="h-8 w-8 text-destructive" aria-hidden="true" />
        )}
      </div>
      <div className="space-y-2">
        <h2 className="text-2xl font-semibold text-foreground">{title}</h2>
        <p className="max-w-lg text-sm leading-7 text-muted-foreground">{message}</p>
      </div>
      {onRetry && (
        <Button onClick={onRetry} variant="outline" size="sm">
          Try Again
        </Button>
      )}
    </div>
  );
}
