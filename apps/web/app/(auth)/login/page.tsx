import { LoginForm } from "./login-form";

export const metadata = {
  title: "Login | EduConnect",
  description: "Login to EduConnect with your phone number and PIN or password",
};

export default function LoginPage(): React.ReactElement {
  return <LoginForm />;
}
