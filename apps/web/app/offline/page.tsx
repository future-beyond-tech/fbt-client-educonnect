import { WifiOff } from "lucide-react";
import { ThemeToggle } from "@/components/shared/theme-toggle";
import { Card, CardContent } from "@/components/ui/card";

export default function OfflinePage(): React.ReactElement {
  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10 text-center">
      <ThemeToggle className="fixed right-4 top-4 z-10" />
      <Card className="max-w-lg">
        <CardContent className="space-y-5 p-8">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full border border-border/70 bg-secondary/70">
            <WifiOff className="h-8 w-8 text-muted-foreground" />
          </div>
          <div className="space-y-2">
            <h1 className="text-3xl font-semibold text-foreground">
              You&apos;re offline
            </h1>
            <p className="max-w-md text-sm leading-7 text-muted-foreground">
              Check your internet connection and try again. Your data will sync
              automatically when you&apos;re back online.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
