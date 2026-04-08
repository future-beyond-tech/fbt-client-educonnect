import { ForgotPasswordForm } from "./forgot-password-form";

export const metadata = {
  title: "Forgot Password | EduConnect",
  description:
    "Request a password reset link for your EduConnect staff account.",
};

export default function ForgotPasswordPage(): React.ReactElement {
  return <ForgotPasswordForm />;
}
