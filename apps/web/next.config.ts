import type { NextConfig } from "next";
import { withSentryConfig } from "@sentry/nextjs";

// Response-invariant security headers applied to every route.
// CSP is emitted from middleware.ts because it needs a per-request nonce.
const securityHeaders = [
  {
    key: "Strict-Transport-Security",
    value: "max-age=63072000; includeSubDomains; preload",
  },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  {
    key: "Permissions-Policy",
    value: "camera=(), microphone=(), geolocation=(), interest-cohort=()",
  },
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  { key: "Cross-Origin-Resource-Policy", value: "same-origin" },
];

const nextConfig: NextConfig = {
  reactStrictMode: true,
  output: "standalone",
  async headers() {
    return [
      {
        source: "/:path*",
        headers: securityHeaders,
      },
    ];
  },
  webpack: (config, { isServer }) => {
    // Suppress critical dependency warnings from OpenTelemetry/Sentry
    // These are caused by dynamic require() usage in Node.js instrumentation
    // and are harmless — they don't affect runtime behavior
    if (isServer) {
      config.ignoreWarnings = [
        ...(config.ignoreWarnings ?? []),
        {
          module: /opentelemetry/,
          message: /Critical dependency/,
        },
        {
          module: /require-in-the-middle/,
          message: /Critical dependency/,
        },
      ];
    }
    return config;
  },
};

const sentryConfig = withSentryConfig(nextConfig, {
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  silent: !process.env.CI,
  widenClientFileUpload: true,
  disableLogger: true,
  automaticVercelMonitors: true,
});

export default process.env.NEXT_PUBLIC_SENTRY_DSN ? sentryConfig : nextConfig;
