import { z } from "zod";

// Single source of truth for the client-side password policy. Mirrors the
// backend PasswordPolicyValidator; keep these two in lockstep.
//
// Login is NOT validated against this schema — users with pre-policy
// passwords must still be able to sign in so the legacy-rotation flow can
// force them to change.

export const PASSWORD_POLICY = {
  minLength: 8,
  maxLength: 128,
} as const;

export const PASSWORD_POLICY_MESSAGE =
  `At least ${PASSWORD_POLICY.minLength} characters, including a letter and a digit.`;

export const passwordSchema = z
  .string()
  .min(PASSWORD_POLICY.minLength, {
    message: `Password must be at least ${PASSWORD_POLICY.minLength} characters.`,
  })
  .max(PASSWORD_POLICY.maxLength, {
    message: `Password must be ${PASSWORD_POLICY.maxLength} characters or fewer.`,
  })
  .regex(/[A-Za-z]/, { message: "Password must contain at least one letter." })
  .regex(/\d/, { message: "Password must contain at least one digit." });

export type PasswordValidationResult =
  | { valid: true }
  | { valid: false; message: string };

// Convenience for forms that aren't wired through a full schema. Returns the
// first failing rule message, matching server behaviour on validation errors.
export function validatePassword(value: string): PasswordValidationResult {
  const result = passwordSchema.safeParse(value);
  if (result.success) return { valid: true };
  return { valid: false, message: result.error.issues[0]?.message ?? "Invalid password." };
}
