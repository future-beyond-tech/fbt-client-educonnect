"use client";

import * as React from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { defaultRouteByRole } from "@/lib/constants";
import { Spinner } from "@/components/ui/spinner";

function getRequiredRole(pathname: string): keyof typeof defaultRouteByRole | null {
  if (pathname.startsWith("/parent/")) {
    return "Parent";
  }

  if (pathname.startsWith("/teacher/")) {
    return "Teacher";
  }

  if (pathname.startsWith("/admin/")) {
    return "Admin";
  }

  return null;
}

export function AuthGuard({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement | null {
  const { user, isLoading } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const requiredRole = getRequiredRole(pathname);
  const canRender = user && (!requiredRole || user.role === requiredRole);

  React.useEffect(() => {
    if (isLoading) {
      return;
    }

    if (!user) {
      router.replace("/login");
      return;
    }

    if (requiredRole && user.role !== requiredRole) {
      router.replace(defaultRouteByRole[user.role]);
    }
  }, [isLoading, requiredRole, router, user]);

  if (!canRender) {
    return (
      <div className="flex min-h-screen items-center justify-center px-4">
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <Spinner size="sm" />
          <span>{isLoading ? "Restoring your session..." : "Redirecting..."}</span>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
