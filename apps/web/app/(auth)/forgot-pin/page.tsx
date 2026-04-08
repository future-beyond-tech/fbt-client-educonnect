import { ForgotPinForm } from "./forgot-pin-form";

export const metadata = {
  title: "Forgot PIN | EduConnect",
  description: "Request a PIN reset link for your EduConnect parent account.",
};

export default function ForgotPinPage(): React.ReactElement {
  return <ForgotPinForm />;
}
