export const JAPAN_PHONE_COUNTRY_LABEL = "JP";
export const JAPAN_PHONE_COUNTRY_CODE = "+81";
export const JAPAN_PHONE_LOCAL_DIGITS = 11;
export const JAPAN_PHONE_VALIDATION_MESSAGE =
  "Phone number must be exactly 11 digits for Japan (+81).";

export function digitsOnly(value: string): string {
  return value.replace(/\D/g, "");
}

export function isValidJapanPhone(value: string): boolean {
  const digits = digitsOnly(value);

  return (
    digits.length === JAPAN_PHONE_LOCAL_DIGITS ||
    (digits.startsWith("81") &&
      digits.length === JAPAN_PHONE_LOCAL_DIGITS + 2)
  );
}

export function normalizeJapanPhoneInput(value: string): string {
  const digits = digitsOnly(value);

  if (digits.startsWith("81")) {
    return digits.slice(2, JAPAN_PHONE_LOCAL_DIGITS + 2);
  }

  return digits.slice(0, JAPAN_PHONE_LOCAL_DIGITS);
}

export function normalizeJapanPhoneSearch(value: string): string {
  const digits = digitsOnly(value);

  return digits.startsWith("81") ? digits.slice(2) : digits;
}
