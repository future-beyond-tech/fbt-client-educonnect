import { Suspense } from "react";
import { ResetPasswordForm } from "./reset-password-form";

export const metadata = {
  title: "Reset Password | EduConnect",
  description: "Choose a new password for your EduConnect account.",
};

export default function ResetPasswordPage(): React.ReactElement {
  return (
    <Suspense
      fallback={
        <p className="text-sm text-muted-foreground">Loading reset form...</p>
      }
    >
      <ResetPasswordForm />
    </Suspense>
  );
}
