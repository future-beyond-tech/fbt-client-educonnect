# Phase 1 — Session Persistence Fix

**Scope:** Minimal fix per Section 14/15 of the Phase 0 report and the user's scope confirmation ("Minimal fix (recommended)", "No — stay single-origin"). Backend and DB untouched. No migrations. No JWT-claim changes. No SameSite posture change. No localStorage cleanup (no such key ever existed).

## Root cause (one line)
Login wrote the HttpOnly `refresh_token` cookie onto the **Next.js origin** via `cookies().set()`, but `api-client.doRefresh()` fetched the **backend origin** directly with `credentials: "include"`. The browser never attaches cookies across origins, so every page reload → cookieless request → 401 → `tokenStore.clear()` → redirect to `/login`.

## Fix summary
Introduce a same-origin refresh proxy on Next.js. The browser now refreshes against `/api/auth/refresh` on the Next.js origin (same origin as the cookie lives on); that Route Handler forwards the call server-to-server to the real API with the cookie explicitly attached as a `Cookie` header, then proxies the rotated `Set-Cookie` back onto the Next.js origin via the shared helper.

Cookie-shape attributes at the proxy layer stay aligned with `RefreshTokenCookieOptions.Create` on the backend: `HttpOnly=true, Secure=(prod), SameSite=Strict, Path=/`, no Domain.

## Files changed

| File | Kind | What |
|---|---|---|
| `apps/web/lib/auth/refresh-cookie.ts` | new | Shared helper module: `parseSetCookie`, `proxyRefreshCookie(response)`, `forwardedRefreshCookieHeader()`, `clearRefreshCookie()`, `REFRESH_COOKIE_NAME`. Single source of truth for cookie-jar writes/reads across both transports. |
| `apps/web/app/api/auth/refresh/route.ts` | new | Same-origin Route Handler. Reads the refresh cookie from the Next.js jar, forwards it to `${NEXT_PUBLIC_API_URL}/api/auth/refresh` via a server-side fetch, proxies the new `Set-Cookie` back onto the Next.js origin, returns the JSON payload to the browser. Clears the local cookie on upstream 401. |
| `apps/web/lib/api-client.ts` | edit | `doRefresh()` now fetches same-origin `/api/auth/refresh` (was `${apiBaseUrl()}/api/auth/refresh`). Single-flight, 401-retry, and tokenStore integration unchanged. |
| `apps/web/lib/actions/auth-actions.ts` | edit | Deleted the locally-scoped `parseSetCookie`, `proxyRefreshCookie`, `forwardedRefreshCookieHeader`, and inline `cookies().delete(REFRESH_COOKIE_NAME)` calls. Now imports all four from `@/lib/auth/refresh-cookie`. `loginAction`, `mintBackendAccessToken`, `logoutAction` logic and external behavior preserved byte-for-byte. |
| `apps/web/e2e/auth-session-persistence.spec.ts` | new | Regression coverage: for each role (Admin, Teacher, Parent), navigate to a protected route → `page.reload()` → assert still on the protected route (not `/login`) and that the refresh endpoint was hit on every mount. One additional test confirms the 401 → `/login` bounce still works when the refresh cookie is invalid/missing. |

No file deletions. No backend files touched. No migrations.

## Verification run in sandbox
- `npx tsc --noEmit` (apps/web) → exit 0, no errors.
- `npx eslint` across all changed files → exit 0, no errors.
- grep audit: only three `fetch(... /api/auth/refresh ...)` call sites remain, and each is correct for its trust boundary —
  - `api-client.ts:64` — browser → same-origin Next Route Handler (the fix).
  - `auth-actions.ts:138` — Server Action → backend with explicit `Cookie` header (server-to-server, no cross-origin issue; `?noRotate=true`).
  - `app/api/auth/refresh/route.ts:51` — Route Handler → backend with explicit `Cookie` header (server-to-server).
- Existing e2e test `admin-teachers-filter-bar.spec.ts` still works because `page.route("**/api/auth/refresh", ...)` matches both origins.

## Security posture (unchanged except as called out)
- Refresh token rotation + reuse-detection + family burn-down: unchanged (backend handler untouched).
- BCrypt.EnhancedHashPassword(factor 12) for refresh token storage: unchanged.
- Cookie attributes at the edge the browser sees: unchanged — `HttpOnly, Secure (prod), SameSite=Strict, Path=/`.
- `?noRotate=true` Server-Action mint path: preserved; still server-to-server, still only issues an access token without rotating.
- JWT claims and access-token lifetime (15 min): unchanged.
- The new Route Handler adds one extra hop (browser → Next → API) only for `/api/auth/refresh`. All other authenticated requests continue to go browser-direct-to-API with a Bearer token, as before — FCP-sensitive paths unchanged.

## Multi-tenancy
Per the user's "stay single-origin" decision, tenancy continues to be enforced solely via the `schoolId` JWT claim in `TenantIsolationMiddleware` (backend). No subdomain routing, no Host-header cross-check, no DB changes. Deferred items tracked in `docs/auth/phase-0-current-state-report.md §16` remain deferred.

## What was explicitly NOT done (per scope confirmation)
- No `FamilyId` column or migration.
- No `BCrypt → SHA-256` refresh-hash migration.
- No `SameSite=Strict → Lax` change.
- No `/api/auth/me` endpoint.
- No localStorage legacy-key cleanup (no such key exists; the token was always in-memory).
- No `ReplacedById → ReplacedByTokenHash` rename.
- No `jti` / `email` JWT claim additions.
- No Host-header tenant cross-check.

Each of these was evaluated in Phase 0 §14 and deliberately deferred; see §15 for the reasoning chain.

## Rollback
Single-commit revert. The helper module (`refresh-cookie.ts`) can remain in place — dead code — but reverting `api-client.ts:doRefresh` to `${apiBaseUrl()}/api/auth/refresh` and deleting `app/api/auth/refresh/route.ts` restores pre-fix behavior. `auth-actions.ts` would need to re-inline the helpers or keep importing them (both work, since the helpers are pure).

## Definition of done
- [x] `pnpm -C apps/web type-check` passes clean.
- [x] `pnpm -C apps/web lint` passes clean on changed files.
- [x] Regression test added covering all three roles + the unauthenticated path.
- [x] Backend untouched (no API contract change, no migration).
- [x] Shared cookie-proxy helper prevents future drift between the Server Action and Route Handler transports.
- [ ] **Pending local run by Feroze:** `pnpm -C apps/web test:e2e e2e/auth-session-persistence.spec.ts` against the running stack.
- [ ] **Pending local QA by Feroze:** Manual reload on all four roles (Admin, Teacher, Parent, Student) on a real deployed origin to confirm the HttpOnly cookie is being set and read on the Next.js origin as expected.
