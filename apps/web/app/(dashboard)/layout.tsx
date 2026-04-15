import { Suspense } from "react";
import { Header } from "@/components/layout/header";
import { Sidebar } from "@/components/layout/sidebar";
import { BottomNav } from "@/components/layout/bottom-nav";
import { Skeleton } from "@/components/ui/skeleton";
import { AuthGuard } from "@/components/auth/auth-guard";
import { RetentionSlot } from "@/components/shared/retention-slot";

function DashboardSkeleton(): React.ReactElement {
  return (
    <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 pb-24 pt-6 md:px-8 md:pb-10 md:pt-8">
      <Skeleton className="h-44 w-full rounded-[32px]" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton className="h-40 rounded-[28px]" />
        <Skeleton className="h-40 rounded-[28px]" />
        <Skeleton className="h-40 rounded-[28px]" />
      </div>
    </div>
  );
}

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  return (
    <AuthGuard>
      <div className="relative flex min-h-screen flex-col">
        <div className="pointer-events-none fixed inset-0 -z-10 bg-[radial-gradient(circle_at_top_left,rgb(var(--glow-1)/0.14),transparent_26rem),radial-gradient(circle_at_top_right,rgb(var(--glow-2)/0.14),transparent_24rem),radial-gradient(circle_at_70%_28%,rgb(var(--glow-3)/0.11),transparent_30rem),linear-gradient(180deg,rgb(var(--page-wash)/0.84),transparent_22rem)] dark:bg-[radial-gradient(circle_at_top_left,rgb(var(--glow-1)/0.14),transparent_28rem),radial-gradient(circle_at_top_right,rgb(var(--glow-3)/0.18),transparent_26rem),linear-gradient(180deg,rgb(var(--page-wash)/0.96),rgb(var(--page-wash)/0.62)_26rem,transparent_34rem)]" />
        <Sidebar />
        <Header />
        <main className="flex-1 pb-24 md:ml-[19rem] md:pb-8">
          <RetentionSlot />
          <Suspense fallback={<DashboardSkeleton />}>
            {children}
          </Suspense>
        </main>
        <BottomNav />
      </div>
    </AuthGuard>
  );
}
