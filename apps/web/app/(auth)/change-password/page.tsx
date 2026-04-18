import { ChangePasswordForm } from "./change-password-form";

export const metadata = {
  title: "Change Password | EduConnect",
  description: "Set a new password for your EduConnect staff account.",
};

export default function ChangePasswordPage(): React.ReactElement {
  return <ChangePasswordForm />;
}
