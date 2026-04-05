export function validateEnv(): void {
  const requiredEnvVars = ["NEXT_PUBLIC_API_URL", "NEXT_PUBLIC_APP_URL"];

  const missingEnvVars = requiredEnvVars.filter((envVar) => {
    const value =
      typeof process !== "undefined" && process.env
        ? process.env[envVar]
        : undefined;
    return !value;
  });

  if (missingEnvVars.length > 0) {
    throw new Error(
      `Missing required environment variables: ${missingEnvVars.join(", ")}`
    );
  }
}
