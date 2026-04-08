import { Suspense } from "react";
import { ResetPinForm } from "./reset-pin-form";

export const metadata = {
  title: "Reset PIN | EduConnect",
  description: "Choose a new PIN for your EduConnect parent account.",
};

export default function ResetPinPage(): React.ReactElement {
  return (
    <Suspense
      fallback={
        <p className="text-sm text-muted-foreground">Loading reset form...</p>
      }
    >
      <ResetPinForm />
    </Suspense>
  );
}
