import { WifiOff } from "lucide-react";

export default function OfflinePage(): React.ReactElement {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center px-4 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-muted-100">
        <WifiOff className="h-8 w-8 text-muted-foreground" />
      </div>
      <h1 className="mt-6 text-2xl font-bold text-foreground">
        You&apos;re offline
      </h1>
      <p className="mt-2 max-w-sm text-muted-foreground">
        Check your internet connection and try again. Your data will sync
        automatically when you&apos;re back online.
      </p>
    </div>
  );
}
