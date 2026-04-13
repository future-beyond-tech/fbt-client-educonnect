import { APP_NAME } from "@/lib/constants";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ThemeToggle } from "@/components/shared/theme-toggle";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  return (
    <div className="relative min-h-screen overflow-hidden px-4 py-10 lg:px-8">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(22,88,136,0.18),transparent_28rem),radial-gradient(circle_at_bottom_right,rgba(58,199,179,0.18),transparent_24rem),radial-gradient(circle_at_center,rgba(255,176,32,0.12),transparent_30rem)] dark:bg-[radial-gradient(circle_at_top_left,rgba(82,174,236,0.14),transparent_30rem),radial-gradient(circle_at_bottom_right,rgba(85,219,199,0.16),transparent_24rem),radial-gradient(circle_at_center,rgba(13,41,64,0.42),transparent_34rem)]" />
      <ThemeToggle className="absolute right-4 top-4 z-10 lg:right-8 lg:top-8" />
      <div className="relative mx-auto flex min-h-[calc(100vh-5rem)] max-w-7xl flex-col justify-center gap-8 lg:flex-row lg:items-center lg:gap-10">
        <section className="flex-1 space-y-6 lg:max-w-2xl">
          <span className="inline-flex rounded-full border border-primary/15 bg-card/72 px-4 py-2 text-xs font-semibold uppercase tracking-[0.26em] text-primary/80 shadow-[0_14px_36px_-26px_rgba(15,23,42,0.55)] backdrop-blur-sm dark:bg-card/84">
            Connected campus workflow
          </span>
          <div className="space-y-4">
            <h1 className="max-w-xl text-4xl font-semibold text-foreground md:text-5xl">
              {APP_NAME} keeps parents, teachers, and admins in sync.
            </h1>
            <p className="max-w-2xl text-base leading-8 text-muted-foreground">
              Sign in to manage attendance, publish homework, send notices, and
              stay close to every update that matters.
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_18px_48px_-30px_rgba(15,23,42,0.45)] backdrop-blur-sm dark:bg-card/86">
              <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-muted-foreground">
                Parents
              </p>
              <p className="mt-2 text-lg font-semibold text-foreground">
                Attendance, leave, notices
              </p>
            </div>
            <div className="rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_18px_48px_-30px_rgba(15,23,42,0.45)] backdrop-blur-sm dark:bg-card/86">
              <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-muted-foreground">
                Teachers
              </p>
              <p className="mt-2 text-lg font-semibold text-foreground">
                Homework and classroom flow
              </p>
            </div>
            <div className="rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_18px_48px_-30px_rgba(15,23,42,0.45)] backdrop-blur-sm dark:bg-card/86">
              <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-muted-foreground">
                Admins
              </p>
              <p className="mt-2 text-lg font-semibold text-foreground">
                Student and staff operations
              </p>
            </div>
          </div>
        </section>

        <Card className="relative w-full max-w-xl overflow-hidden">
          <div className="pointer-events-none absolute inset-x-0 top-0 h-24 bg-[radial-gradient(circle_at_top,rgba(58,199,179,0.18),transparent_70%)]" />
          <CardHeader className="relative space-y-3 border-b border-border/50 bg-card/35 text-center dark:bg-card/55">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-[22px] bg-[linear-gradient(135deg,rgb(var(--primary)),rgb(var(--primary-strong)))] text-lg font-semibold text-primary-foreground shadow-[0_18px_42px_-22px_rgba(12,57,95,0.82)]">
              {APP_NAME.slice(0, 2)}
            </div>
            <div className="space-y-2">
              <CardTitle className="text-3xl">{APP_NAME}</CardTitle>
              <CardDescription>
                Secure access for every role in your school community.
              </CardDescription>
            </div>
          </CardHeader>
          <CardContent className="relative p-6 md:p-8">
            {children}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
