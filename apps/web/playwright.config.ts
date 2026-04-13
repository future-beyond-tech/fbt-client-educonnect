import { defineConfig, devices } from "@playwright/test";

const port = process.env.PLAYWRIGHT_PORT ?? "3100";
const baseURL =
  process.env.PLAYWRIGHT_BASE_URL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [["list"]],
  use: {
    baseURL,
    trace: "on-first-retry",
  },
  projects: [
    {
      name: "chromium-mobile",
      use: { ...devices["Pixel 5"] },
    },
  ],
  webServer: {
    command: `NEXT_PUBLIC_APP_URL=${baseURL} NEXT_PUBLIC_API_URL=http://127.0.0.1:5000 pnpm exec next build && NEXT_PUBLIC_APP_URL=${baseURL} NEXT_PUBLIC_API_URL=http://127.0.0.1:5000 HOSTNAME=127.0.0.1 PORT=${port} node .next/standalone/apps/web/server.js`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 300_000,
    stdout: "pipe",
    stderr: "pipe",
  },
});
