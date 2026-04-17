import { APP_NAME, SCHOOL_NAME } from "@/lib/constants";
import { Card, CardContent, CardDescription, CardHeader } from "@/components/ui/card";
import { ThemeToggle } from "@/components/shared/theme-toggle";
import { Aurora } from "@/components/effects/aurora";
import { SplitText } from "@/components/effects/split-text";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  return (
    <div className="relative min-h-screen overflow-hidden px-4 py-10 lg:px-8">
      <Aurora intensity={0.9} />
      <ThemeToggle className="absolute right-4 top-4 z-10 lg:right-8 lg:top-8" />
      <div className="relative mx-auto flex min-h-[calc(100vh-5rem)] max-w-xl flex-col justify-center">
        <Card className="relative w-full overflow-hidden">
          <div className="pointer-events-none absolute inset-x-0 top-0 h-24 bg-[radial-gradient(circle_at_top,rgb(var(--glow-1)/0.18),transparent_70%)]" />
          <CardHeader className="relative space-y-3 border-b border-border/50 bg-card/35 text-center dark:bg-card/55">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-[18px] bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--accent)))] text-base font-semibold text-primary-foreground shadow-[0_18px_42px_-26px_rgba(15,40,69,0.55)]">
              {APP_NAME.slice(0, 2)}
            </div>
            <div className="space-y-1.5">
              <h2
                aria-label={SCHOOL_NAME}
                className="rainbow-text text-xl font-extrabold tracking-tight md:text-2xl"
              >
                {SCHOOL_NAME}
              </h2>
              <SplitText
                as="h1"
                text={APP_NAME}
                className="bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--primary-strong)))] bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl"
              />
              <CardDescription className="mx-auto max-w-sm">
                Sign in to continue.
              </CardDescription>
            </div>
          </CardHeader>
          <CardContent className="relative p-6 md:p-8">{children}</CardContent>
        </Card>
      </div>
    </div>
  );
}
