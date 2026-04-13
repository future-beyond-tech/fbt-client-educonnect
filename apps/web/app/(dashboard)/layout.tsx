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
        <div className="pointer-events-none fixed inset-0 -z-10 bg-[radial-gradient(circle_at_top_left,rgba(58,199,179,0.12),transparent_24rem),radial-gradient(circle_at_top_right,rgba(255,176,32,0.16),transparent_22rem),linear-gradient(180deg,rgba(255,255,255,0.78),transparent_22rem)] dark:bg-[radial-gradient(circle_at_top_left,rgba(85,219,199,0.14),transparent_26rem),radial-gradient(circle_at_top_right,rgba(82,174,236,0.18),transparent_24rem),linear-gradient(180deg,rgba(8,18,31,0.96),rgba(8,18,31,0.58)_26rem,transparent_34rem)]" />
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
