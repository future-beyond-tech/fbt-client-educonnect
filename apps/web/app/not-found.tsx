import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export default function NotFound(): React.ReactElement {
  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <Card className="max-w-lg">
        <CardContent className="space-y-5 p-8 text-center">
          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.28em] text-primary/80">
              404
            </p>
            <h1 className="text-4xl font-semibold text-foreground">Page not found</h1>
            <p className="text-sm leading-7 text-muted-foreground">
              The page you are looking for doesn&apos;t exist or may have moved.
            </p>
          </div>
          <div className="flex justify-center">
            <Link href="/login">
              <Button>Go back to login</Button>
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
