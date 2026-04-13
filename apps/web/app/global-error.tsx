"use client";

import { captureException } from "@sentry/nextjs";
import { useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}): React.ReactElement {
  useEffect(() => {
    captureException(error);
  }, [error]);

  return (
    <html lang="en">
      <body>
        <div className="flex min-h-screen items-center justify-center px-4 py-10">
          <Card className="max-w-lg">
            <CardContent className="space-y-5 p-8 text-center">
              <div className="space-y-2">
                <p className="text-xs font-semibold uppercase tracking-[0.28em] text-destructive">
                  Critical error
                </p>
                <h1 className="text-3xl font-semibold text-foreground">
                  Something went wrong
                </h1>
                <p className="text-sm leading-7 text-muted-foreground">
                  An unexpected error occurred. Our team has been notified.
                </p>
              </div>
              <div className="flex justify-center">
                <Button type="button" onClick={reset}>
                  Try Again
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </body>
    </html>
  );
}
