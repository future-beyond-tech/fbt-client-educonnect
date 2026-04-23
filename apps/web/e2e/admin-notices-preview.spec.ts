import { test, expect, type Page } from "@playwright/test";

/**
 * Coverage for the draft-notice flow introduced alongside the required
 * preview page:
 *   - `Preview & publish` replaces direct publish on the draft card
 *   - `Manage attachments` preloads the draft's current files
 *   - The preview page renders inline previews for Available attachments
 *     and status rows for Pending/ScanFailed
 *   - Publish is allowed from the preview page even with unresolved files,
 *     with a warning banner shown
 *
 * All API calls are stubbed so the spec runs without a backend.
 */

const SCHOOL_ID = "00000000-0000-0000-0000-00000000aaaa";
const USER_ID = "00000000-0000-0000-0000-00000000bbbb";
const DRAFT_NOTICE_ID = "00000000-0000-0000-0000-00000000dddd";

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
  return `${header}.${payload}.signature`;
}

async function primeAuth(page: Page): Promise<void> {
  const token = buildFakeAdminJwt();
  await page.route("**/api/auth/refresh", async (route) => {
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
}

function draftCapabilities() {
  return {
    canEditDraft: true,
    canManageDraftAttachments: true,
    canPreviewDraft: true,
    canPublishDraft: true,
  };
}

function draftNoticePayload() {
  return {
    noticeId: DRAFT_NOTICE_ID,
    title: "Sports day schedule",
    body: "See attached schedule for sports day.",
    targetAudience: "All",
    targetClasses: [],
    isPublished: false,
    publishedAt: null,
    expiresAt: null,
    createdAt: new Date().toISOString(),
    capabilities: draftCapabilities(),
  };
}

async function stubListAndClasses(page: Page): Promise<void> {
  await page.route(/\/api\/classes(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([]),
    });
  });

  await page.route(/\/api\/notices(\?|$)/, async (route) => {
    if (route.request().method() !== "GET") {
      await route.continue();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([draftNoticePayload()]),
    });
  });
}

async function stubPreview(
  page: Page,
  attachments: Array<{
    id: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    downloadUrl: string;
    uploadedAt: string;
    status: "Available" | "Pending" | "ScanFailed" | "Infected";
  }>
): Promise<void> {
  await page.route(`**/api/notices/${DRAFT_NOTICE_ID}`, async (route) => {
    if (route.request().method() !== "GET") {
      await route.continue();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(draftNoticePayload()),
    });
  });

  await page.route(/\/api\/attachments\?entityId=/, async (route) => {
    if (route.request().method() !== "GET") {
      await route.continue();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(attachments),
    });
  });
}

test.describe("admin notices preview flow", () => {
  test("draft card exposes Manage attachments and Preview & publish", async ({
    page,
  }) => {
    await primeAuth(page);
    await stubListAndClasses(page);

    await page.goto("/admin/notices");

    await expect(page.getByText("Sports day schedule")).toBeVisible();
    await expect(
      page.getByRole("button", {
        name: "Manage attachments for Sports day schedule",
      })
    ).toBeVisible();
    await expect(
      page.getByRole("link", {
        name: "Preview and publish Sports day schedule",
      })
    ).toBeVisible();
    // Direct Publish button must be gone from the draft card.
    await expect(
      page.getByRole("button", { name: "Publish", exact: true })
    ).toHaveCount(0);
  });

  test("preview page renders inline image preview and warns for unresolved attachments", async ({
    page,
  }) => {
    await primeAuth(page);
    await stubListAndClasses(page);
    await stubPreview(page, [
      {
        id: "a-available",
        fileName: "clean.png",
        contentType: "image/png",
        sizeBytes: 1024,
        downloadUrl: "https://example.test/clean.png",
        uploadedAt: new Date().toISOString(),
        status: "Available",
      },
      {
        id: "a-pending",
        fileName: "still-scanning.pdf",
        contentType: "application/pdf",
        sizeBytes: 2048,
        downloadUrl: "",
        uploadedAt: new Date().toISOString(),
        status: "Pending",
      },
      {
        id: "a-scan-failed",
        fileName: "broken.pdf",
        contentType: "application/pdf",
        sizeBytes: 4096,
        downloadUrl: "",
        uploadedAt: new Date().toISOString(),
        status: "ScanFailed",
      },
    ]);

    await page.goto(`/admin/notices/${DRAFT_NOTICE_ID}/preview`);

    await expect(
      page.getByRole("heading", { name: "Preview notice" })
    ).toBeVisible();

    // Inline image preview rendered for Available attachment only.
    await expect(page.getByAltText("clean.png")).toBeVisible();

    // Pending/ScanFailed rows show status badges, not inline PDF previews.
    await expect(page.getByText("still-scanning.pdf")).toBeVisible();
    await expect(page.getByText("Scanning…")).toBeVisible();
    await expect(page.getByText("broken.pdf")).toBeVisible();
    await expect(page.getByText("Scan failed")).toBeVisible();

    // Warning banner calls out unresolved attachments.
    await expect(
      page.getByText(/Unresolved files are not previewable/i)
    ).toBeVisible();

    // Publish is still offered.
    await expect(
      page.getByRole("button", { name: "Publish" })
    ).toBeEnabled();

    // Back link exists.
    await expect(
      page.getByRole("link", { name: "Back to notices" })
    ).toBeVisible();
  });

  test("publish from preview page succeeds even with unresolved attachments", async ({
    page,
  }) => {
    await primeAuth(page);
    await stubListAndClasses(page);
    await stubPreview(page, [
      {
        id: "a-pending",
        fileName: "still-scanning.pdf",
        contentType: "application/pdf",
        sizeBytes: 2048,
        downloadUrl: "",
        uploadedAt: new Date().toISOString(),
        status: "Pending",
      },
    ]);

    let publishCalled = false;
    await page.route(
      `**/api/notices/${DRAFT_NOTICE_ID}/publish`,
      async (route) => {
        publishCalled = true;
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            message: "Notice published successfully. It is now immutable.",
          }),
        });
      }
    );

    await page.goto(`/admin/notices/${DRAFT_NOTICE_ID}/preview`);
    await page.getByRole("button", { name: "Publish" }).click();

    await expect.poll(() => publishCalled).toBeTruthy();
    await expect(page).toHaveURL(/\/admin\/notices$/);
  });
});
