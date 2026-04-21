import { z } from "zod";
import { JAPAN_PHONE_LOCAL_DIGITS } from "@/lib/phone";

// Login input shape. The form submits the same fields for either mode; the
// discriminator 'mode' decides which of password / pin is required.

const phoneSchema = z
  .string()
  .regex(new RegExp(`^\\d{${JAPAN_PHONE_LOCAL_DIGITS}}$`), {
    message: "Phone number must be exactly 11 digits.",
  });

export const staffLoginSchema = z.object({
  mode: z.literal("staff"),
  phone: phoneSchema,
  // Login itself does NOT validate password strength (see Phase 2 —
  // legacy-password users must be able to sign in so we can force them
  // through the change-password flow).
  password: z.string().min(1, "Password is required."),
});

export const parentLoginSchema = z.object({
  mode: z.literal("parent"),
  phone: phoneSchema,
  pin: z.string().regex(/^\d{4,6}$/, "PIN must be 4 to 6 digits."),
});

export const loginSchema = z.discriminatedUnion("mode", [
  staffLoginSchema,
  parentLoginSchema,
]);

export type LoginInput = z.infer<typeof loginSchema>;
