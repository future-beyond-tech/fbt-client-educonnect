"use client";

import * as React from "react";
import { captureException } from "@sentry/nextjs";
import { ErrorState } from "@/components/shared/error-state";

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}): React.ReactElement {
  React.useEffect(() => {
    captureException(error);
  }, [error]);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <ErrorState
        title="Something went wrong"
        message="An unexpected error occurred. Our team has been notified."
        onRetry={reset}
      />
    </div>
  );
}
