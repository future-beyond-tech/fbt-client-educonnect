"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { defaultRouteByRole } from "@/lib/constants";
import { Spinner } from "@/components/ui/spinner";

export default function Home(): React.ReactElement {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (isLoading) return;

    if (user) {
      router.replace(defaultRouteByRole[user.role]);
    } else {
      router.replace("/login");
    }
  }, [isLoading, user, router]);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spinner size="sm" />
    </div>
  );
}
