import { test, expect, type Page, type Route } from "@playwright/test";

/**
 * Regression coverage for the "browser-refresh logs the user out" bug.
 *
 * Root cause (pre-fix): login put the HttpOnly refresh cookie on the
 * Next.js origin, but api-client.refreshAccessToken() fetched the backend
 * origin directly. The browser never attached the cookie to that
 * cross-origin call, so every page reload → 401 → redirect to /login.
 *
 * Fix: refresh now hits a same-origin Next.js Route Handler
 * (/api/auth/refresh) which forwards the cookie server-to-server.
 *
 * This spec verifies, for each role, that:
 *   1. With a valid refresh cookie (simulated by stubbing the same-origin
 *      refresh endpoint to 200), the AuthProvider's bootstrap call keeps
 *      the user on the protected route.
 *   2. After page.reload(), the user is STILL on the protected route —
 *      i.e. the new access token survived the navigation.
 *   3. The refresh endpoint is called on every boot (twice total:
 *      initial mount + post-reload mount).
 *
 * A fourth test confirms the opposite path: when refresh returns 401 the
 * AuthGuard bounces the user to /login — so this fix hasn't regressed
 * the no-session case.
 */

const SCHOOL_ID = "00000000-0000-0000-0000-00000000aaaa";
const USER_ID = "00000000-0000-0000-0000-00000000bbbb";

type Role = "Admin" | "Teacher" | "Parent";

interface RoleFixture {
  role: Role;
  landingPath: string;
  // A selector that is only present AFTER AuthGuard renders its children.
  // If we end up on /login instead, this selector will never resolve.
  dashboardMarker: RegExp | string;
}

const fixtures: RoleFixture[] = [
  {
    role: "Admin",
    landingPath: "/admin/teachers",
    dashboardMarker: /\/admin\/teachers$/,
  },
  {
    role: "Teacher",
    landingPath: "/teacher/homework",
    dashboardMarker: /\/teacher\/homework$/,
  },
  {
    role: "Parent",
    landingPath: "/parent/attendance",
    dashboardMarker: /\/parent\/attendance$/,
  },
];

function base64UrlEncode(value: string): string {
  return Buffer.from(value, "utf8")
    .toString("base64")
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
}

function buildFakeJwt(role: Role, mustChangePassword = false): string {
  const header = base64UrlEncode(JSON.stringify({ alg: "none", typ: "JWT" }));
  const now = Math.floor(Date.now() / 1000);
  const payload = base64UrlEncode(
    JSON.stringify({
      sub: USER_ID,
      userId: USER_ID,
      schoolId: SCHOOL_ID,
      role,
      name: `Test ${role}`,
      must_change_password: mustChangePassword,
      iat: now,
      exp: now + 60 * 60,
    }),
  );
  // Signature is never verified client-side — just present so the token
  // parses as a three-part JWT.
  return `${header}.${payload}.signature`;
}

/**
 * Stubs the same-origin refresh endpoint to return a valid token for
 * `role`. Returns a counter function so tests can assert how many times
 * refresh was called — expected to increment once per page mount.
 */
async function stubRefresh(
  page: Page,
  role: Role,
): Promise<() => number> {
  let callCount = 0;
  const token = buildFakeJwt(role);
  await page.route("**/api/auth/refresh", async (route: Route) => {
    callCount += 1;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        accessToken: token,
        expiresIn: 60 * 60,
        mustChangePassword: false,
      }),
    });
  });
  return (): number => callCount;
}

// Stubs for the handful of protected endpoints the role-landing pages
// call on mount. Anything not explicitly stubbed falls through to the
// default 5000-port backend (which isn't running in e2e), so each
// landing route needs at least one non-erroring stub to render without
// a runtime crash in its data fetcher.
async function stubLandingEndpoints(page: Page): Promise<void> {
  const emptyList = {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  };

  // Admin teachers list + its filter metadata.
  await page.route("**/api/teachers/filter-metadata", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ subjects: [] }),
    });
  });
  await page.route(/\/api\/teachers(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(emptyList),
    });
  });

  // Teacher homework list.
  await page.route(/\/api\/homework(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(emptyList),
    });
  });

  // Parent attendance + "my children" (attendance page fans out to both).
  await page.route(/\/api\/attendance(\?|\/|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(emptyList),
    });
  });
  await page.route("**/api/students/my-children", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([]),
    });
  });

  // Notifications badge polls this on every page — 200 everywhere.
  await page.route("**/api/notifications/unread-count", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ count: 0 }),
    });
  });
}

test.describe("auth session persistence across reload", () => {
  for (const fixture of fixtures) {
    test(`${fixture.role}: session survives a browser refresh`, async ({ page }) => {
      const getRefreshCallCount = await stubRefresh(page, fixture.role);
      await stubLandingEndpoints(page);

      // 1. Initial navigation — bootstrap calls refresh, AuthGuard renders.
      await page.goto(fixture.landingPath);
      await expect(page).toHaveURL(fixture.dashboardMarker);

      // 2. Browser refresh. This is the scenario that used to kick the
      //    user to /login.
      await page.reload();
      await expect(page).toHaveURL(fixture.dashboardMarker);

      // 3. Refresh endpoint was hit on each mount (initial + post-reload).
      //    Pre-emptive refresh fires 120s before expiry (~59min in tests),
      //    so we assert >= 2 rather than exactly 2.
      expect(getRefreshCallCount()).toBeGreaterThanOrEqual(2);
    });
  }

  test("missing refresh cookie: user is redirected to /login", async ({ page }) => {
    // Simulate no valid refresh cookie by returning 401 from the same-
    // origin proxy. The AuthGuard should bounce to /login rather than
    // render the protected page.
    await page.route("**/api/auth/refresh", async (route) => {
      await route.fulfill({
        status: 401,
        contentType: "application/json",
        body: JSON.stringify({ title: "Unauthorized", status: 401 }),
      });
    });

    await page.goto("/admin/teachers");
    await expect(page).toHaveURL(/\/login$/);
  });
});
