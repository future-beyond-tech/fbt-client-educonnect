import { ChangePinForm } from "./change-pin-form";

export const metadata = {
  title: "Change PIN | EduConnect",
  description: "Set a new PIN for your EduConnect parent account.",
};

export default function ChangePinPage(): React.ReactElement {
  return <ChangePinForm />;
}
