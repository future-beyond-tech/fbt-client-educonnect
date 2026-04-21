import { test, expect, type Page } from "@playwright/test";

/**
 * End-to-end coverage for the staff filter bar: URL is the source of truth,
 * chips render for active filters, and "Clear all" returns to the canonical
 * route. All API calls are mocked so the spec runs without a real backend.
 *
 * Playwright runs against `chromium-mobile` by default (see playwright.config.ts);
 * the filter bar collapses to a BottomSheet on mobile, but active chips still
 * render on the main page — which is exactly what this spec asserts.
 */

const SCHOOL_ID = "00000000-0000-0000-0000-00000000aaaa";
const USER_ID = "00000000-0000-0000-0000-00000000bbbb";

function base64UrlEncode(value: string): string {
  return Buffer.from(value, "utf8")
    .toString("base64")
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
}

function buildFakeAdminJwt(): string {
  const header = base64UrlEncode(JSON.stringify({ alg: "none", typ: "JWT" }));
  const now = Math.floor(Date.now() / 1000);
  const payload = base64UrlEncode(
    JSON.stringify({
      sub: USER_ID,
      userId: USER_ID,
      schoolId: SCHOOL_ID,
      role: "Admin",
      name: "Test Admin",
      iat: now,
      exp: now + 60 * 60,
    })
  );
  // The signature is never verified client-side — it's just required to parse
  // as a three-part JWT.
  return `${header}.${payload}.signature`;
}

async function primeAuth(page: Page): Promise<void> {
  const token = buildFakeAdminJwt();
  await page.addInitScript((t) => {
    window.localStorage.setItem("auth_access_token", t);
  }, token);
}

async function stubApi(page: Page): Promise<void> {
  // Refresh endpoint: 401 during the initial session restore keeps the
  // pre-seeded token intact (the AuthProvider guards against wiping on first refresh).
  await page.route("**/api/auth/refresh", async (route) => {
    await route.fulfill({ status: 401, body: "" });
  });

  await page.route("**/api/teachers/filter-metadata", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ subjects: ["English", "Mathematics", "Science"] }),
    });
  });

  await page.route(/\/api\/teachers(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        items: [
          {
            id: "t-1",
            name: "Maya Iyer",
            phone: "09000000001",
            role: "Teacher",
            isActive: true,
            assignedClassCount: 3,
            subjects: ["Mathematics"],
            createdAt: new Date().toISOString(),
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 20,
      }),
    });
  });
}

test.describe("admin staff filter bar", () => {
  test("URL params hydrate visible filter chips", async ({ page }) => {
    await primeAuth(page);
    await stubApi(page);

    await page.goto(
      "/admin/teachers?subjects=Mathematics&load=heavy&sort=classesDesc"
    );

    await expect(page.getByText("Subject: Mathematics")).toBeVisible();
    await expect(page.getByText("Class-load: Heavy (3+)")).toBeVisible();
    await expect(page.getByText("Sort: Most classes")).toBeVisible();
  });

  test("Clear all removes every chip and returns to the canonical URL", async ({
    page,
  }) => {
    await primeAuth(page);
    await stubApi(page);

    await page.goto(
      "/admin/teachers?subjects=Mathematics,Science&load=light&sort=classesDesc"
    );

    await expect(page.getByText("Subject: Mathematics")).toBeVisible();

    await page.getByRole("button", { name: "Clear all" }).first().click();

    await expect(page.getByText("Subject: Mathematics")).toHaveCount(0);
    await expect(page.getByText("Class-load:")).toHaveCount(0);

    await expect(page).toHaveURL(/\/admin\/teachers$/);
  });

  test("Removing a single chip narrows the URL without affecting others", async ({
    page,
  }) => {
    await primeAuth(page);
    await stubApi(page);

    await page.goto(
      "/admin/teachers?subjects=Mathematics,Science&sort=classesDesc"
    );

    await page
      .getByRole("button", { name: "Remove filter: Subject: Mathematics" })
      .click();

    await expect(page.getByText("Subject: Mathematics")).toHaveCount(0);
    await expect(page.getByText("Subject: Science")).toBeVisible();
    await expect(page.getByText("Sort: Most classes")).toBeVisible();

    await expect(page).toHaveURL(/subjects=Science/);
    await expect(page).toHaveURL(/sort=classesDesc/);
    await expect(page).not.toHaveURL(/subjects=Mathematics/);
  });
});
