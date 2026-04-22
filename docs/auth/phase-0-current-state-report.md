# EduConnect — Auth Session Persistence

## Phase 0: Current State Report

**Owner:** Feroze Basha
**Status:** Draft — awaiting approval before Phase 1 edits
**Bug:** Browser refresh logs the user out on all four roles (Admin, Teacher, Parent, Student)

---

## 1. Topology

| Surface      | Location                                                    | Origin (prod, inferred) |
|--------------|-------------------------------------------------------------|-------------------------|
| Frontend     | `apps/web` (Next.js 15.0.7, React 19, App Router)           | e.g. `app.educonnect.app` |
| Backend      | `apps/api/src/EduConnect.Api` (.NET 8, MediatR, EF Core)    | e.g. `api.educonnect.app` |
| DB           | PostgreSQL (Railway), EF Core migrations                    | —                       |

`next.config.ts` has no `rewrites()` entry. Frontend → backend is cross-origin in every environment. `playwright.config.ts` confirms this (`NEXT_PUBLIC_API_URL=http://127.0.0.1:5000`, web on a different port). Dockerfile bakes `NEXT_PUBLIC_API_URL` as a build arg.

`middleware.ts` is **CSP-only** — emits a per-request nonce and security headers. It does not read cookies, does not guard routes, and does not attempt silent refresh.

---

## 2. Token Storage — Current

| Token          | Where it lives                                                                                          |
|----------------|---------------------------------------------------------------------------------------------------------|
| Access (JWT)   | **In-memory only.** Module-scoped `accessToken` in `apps/web/lib/auth/token-store.ts`. Mirrored into React via `useSyncExternalStore`. Never written to `localStorage`, `sessionStorage`, or any cookie. |
| Refresh        | `refresh_token` HttpOnly cookie. Set in two places, **each scoped to a different origin** (see §5).    |

There is **no `localStorage` or `sessionStorage` write of any auth token anywhere in the codebase.** The "migrate legacy localStorage tokens" item in the brief (Phase 3) is therefore a no-op.

---

## 3. Access Token Details

Minted by `Common/Auth/JwtTokenService.GenerateAccessToken`:

- Algorithm: **HS256** (symmetric).
- Key: `JWT_SECRET` env var, read via `IConfiguration`. **No `kid` header. No key rotation strategy.** Single static secret.
- Lifetime: **15 minutes** (`JwtTokenService.AccessTokenLifetimeMinutes`).
- Validated with `ClockSkew = TimeSpan.Zero` on both `Program.cs` bearer config and `JwtTokenService.ValidateToken`.
- Claims: `sub`, `userId`, `schoolId`, `role`, `name`, `must_change_password`, `iat`, `exp`. **No `jti`.**
- Issuer / Audience validated (`JWT_ISSUER`, `JWT_AUDIENCE`).

---

## 4. Refresh Token Infrastructure — already sophisticated

This is important: **a full rotation + reuse-detection system already exists**. The bug is not "there is no refresh flow." The flow exists but one edge is broken.

### 4.1 `refresh_tokens` table

Added in migration `20260415184241_InitialCreate`. Columns: `id`, `user_id`, `school_id`, `token_hash`, `expires_at`, `is_revoked`, `revoked_at`, `created_at`, `replaced_by_id`. Indexes: `user_id`, `school_id`, `expires_at`, `(user_id, is_revoked) WHERE is_revoked = false`. No dedicated `FamilyId`, `UserAgent`, or `IpAddress`.

### 4.2 Token format and hashing

- Composite format: `{guid:N}.{secret}` — 32-hex id + 256-bit secret, joined by a dot (`JwtTokenService.BuildRefreshToken`).
- Secret: 32 random bytes from `RandomNumberGenerator`, base64-encoded.
- Stored as **BCrypt.EnhancedHashPassword(secret, workFactor: 12)**. Never plaintext.
- Lookup on refresh is an O(1) PK fetch on `id`, then `BCrypt.EnhancedVerify(secret, hash)` for the secret.

### 4.3 Rotation, reuse detection, and family revocation

`Features/Auth/RefreshToken/RefreshTokenCommandHandler.cs`:

- Fetches the row by `id` **without** an `IsRevoked` filter — so a replay of an already-rotated token is observable.
- If `IsRevoked == true`: **burn all active tokens for the user** (SELECT + update all `rt.UserId == storedToken.UserId && !rt.IsRevoked && rt.ExpiresAt > now`). Logged at `Warning`: `"Refresh token reuse detected for user {UserId}; revoked {FamilySize} active token(s)"`. Returns 401.
- If `ExpiresAt <= now`: 401, no revocation.
- If `user.IsActive == false`: 401.
- Happy path: mark old `IsRevoked=true, RevokedAt=now`; insert new row with `ReplacedById=old.Id`. Persist. Return `{ AccessToken, ExpiresIn, NewRefreshToken, MustChangePassword }`.

The family is **per-user, not per-chain**. Reuse detected on any device revokes all devices.

### 4.4 `?noRotate=true` mode

Server Actions use `POST /api/auth/refresh?noRotate=true` (`auth-actions.ts:mintBackendAccessToken`). This mints a fresh access token **without** revoking the presented refresh token, so parallel Server Actions don't trigger reuse detection against their own rotations. Commented in both the backend handler and the frontend action. The rotating path (browser side-channel) still runs.

---

## 5. Refresh Cookie — the cookie is set on two origins

This is where the bug originates.

### 5.1 Attributes

Both writers use `RefreshTokenCookieOptions.Create`:

```
HttpOnly = true
Secure   = request.IsHttps
SameSite = Strict
Path     = "/"
Domain   = <not set>          ← host-only
Expires  = now + 7 days
```

### 5.2 Writer A — the .NET API

`LoginCommandHandler.cs:108` and `RefreshTokenEndpoint.cs:30`:

```csharp
httpContext.Response.Cookies.Append(
    "refresh_token", refreshToken,
    RefreshTokenCookieOptions.Create(httpContext.Request, expiresAt));
```

Because `Domain` is absent, this sets a **host-only cookie on the API origin** (e.g. `api.educonnect.app`).

### 5.3 Writer B — Next.js Server Action re-issue

`apps/web/lib/actions/auth-actions.ts:proxyRefreshCookie` reads the upstream `Set-Cookie` header and re-issues via `cookies().set()`:

```ts
cookieStore.set({
  name: parsed.name,
  value: parsed.value,
  httpOnly: true,
  secure: process.env.NODE_ENV === "production",
  sameSite: "strict",
  path: "/",
  expires: parsed.expires,
});
```

No `domain` argument. The cookie is **host-only on the Next.js origin** (e.g. `app.educonnect.app`).

### 5.4 The consequence

The browser ends up with **one `refresh_token` cookie scoped to the Next.js origin only**. The cookie is **never** installed on the API origin because:

1. Login is a Server Action — the browser never makes a direct request to `<API>/api/auth/login` that could receive the API's own `Set-Cookie`.
2. The API's `Set-Cookie` header is consumed server-to-server by the Next.js runtime and re-emitted onto the Next.js response.

---

## 6. Boot Rehydration Flow — current

Root layout (`app/layout.tsx`) is **not async for session**. It renders `<AuthProvider>` without reading cookies or calling the API. There is no `getSession()` / `MeQuery` / `initialUser` plumbing.

`AuthProvider` (`apps/web/providers/auth-provider.tsx:99`):

```ts
useEffect(() => {
  (async () => {
    await refreshAccessToken();
    if (!cancelled) setIsLoading(false);
  })();
}, []);
```

`refreshAccessToken` (`apps/web/lib/api-client.ts:57`):

```ts
fetch(`${NEXT_PUBLIC_API_URL}/api/auth/refresh`, {
  method: "POST",
  credentials: "include",
  headers: { "Content-Type": "application/json" },
});
```

This is a **cross-origin browser fetch** from the Next.js origin to the API origin. Per §5.4, the `refresh_token` cookie is not on the API origin, so the browser attaches nothing. The API endpoint (`RefreshTokenEndpoint.cs:13`) returns `Results.Unauthorized()` immediately. `doRefresh` clears `tokenStore`. `AuthProvider` finishes with `user == null`. `AuthGuard` in `app/(dashboard)/layout.tsx` redirects to `/login`.

**This is the root cause of the logout-on-refresh bug.**

Corollary: the 120-s pre-emptive refresh (`auth-provider.tsx:112`) and the 401-retry refresh (`api-client.ts:131`) are **also non-functional** for the same reason. They haven't been noticed because the 15-min in-memory access token covers any session that never reloads.

---

## 7. Why the escape hatches still work

- **Logout** (`auth-actions.ts:logoutAction`) uses `forwardedRefreshCookieHeader()` — reads the cookie from the Next.js cookie store and forwards it to the API as a `Cookie` header via a server-to-server fetch. Same-origin on the Next side, so the cookie is readable. Works.
- **Server-action → backend calls** (`mintBackendAccessToken` and everything that uses `callBackend`) — same mechanism. Works.

The bug is **only** on the browser → API direct path.

---

## 8. Protected route guarding

`app/(dashboard)/layout.tsx` wraps everything in `<AuthGuard>` (`components/auth/auth-guard.tsx`):

- Reads `user` and `isLoading` from `useAuth`.
- While `isLoading`: renders "Restoring your session…" spinner.
- If `!user`: `router.replace("/login")`.
- If `user.mustChangePassword`: redirects to `/change-pin` or `/change-password` by role.
- Role-based gating by pathname prefix (`/parent/`, `/teacher/`, `/admin/`).

Correct in isolation; it's the AuthProvider's `user` that stays `null` because of §6.

---

## 9. Multi-Tenant Model

Purely **JWT-claim driven**. There is **no** subdomain, path prefix, or Host-header tenant routing.

- `TenantIsolationMiddleware` reads `schoolId` off `context.User` claims after JWT bearer authentication and sets it on `CurrentUserService`.
- `AppDbContext` applies global query filters by `CurrentUserService.SchoolId`.
- Endpoints explicitly whitelisted as unauthenticated: `/health`, `/api/auth/login`, `/api/auth/login-parent`, **`/api/auth/refresh`**. The refresh endpoint's "identity" comes from the cookie alone.

**Implication for the acceptance criterion "session on tenant A cannot access tenant B":** because tenant boundary is in the JWT claim, a `refresh_token` cookie is implicitly bound to a single `schoolId` via its `UserId`. An attacker who steals a refresh cookie inherits only that user's `schoolId`. There is no Host-header cross-check — so if the system ever adopts subdomain-per-tenant, we must add one.

---

## 10. Service Worker

`apps/web/public/sw.js` (custom, no Workbox/`next-pwa`). Pre-caches `/login` and `/offline`. Uses network-first for `/api/*` and cache-first for static assets. **Does not intercept `/api/auth/refresh` responses** beyond passing them through, and does not strip `credentials`. Not the cause of the bug; will re-verify in Phase 1 regression.

---

## 11. Logging

- Backend: **Serilog**. Structured JSON file sink, console, optional Sentry. Destructuring policy redacts `password`, `pin`, `token`, `jwt_secret`, `api_key`. `TenantIsolationMiddleware` pushes `UserId`, `SchoolId`, `Role` into `LogContext`.
- Frontend: **No Pino** installed. `@sentry/nextjs` for error tracking. There is no server-side structured auth-event log on the frontend today.

---

## 12. Answers to Section 10 of the brief

| #  | Question                                         | Answer                                                                                                                                                                               |
|----|--------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1  | Multi-tenant resolution mechanism                | JWT claim `schoolId`. No subdomain / path prefix / Host-header routing.                                                                                                              |
| 2  | Existing refresh-token table or mechanism        | Yes. `refresh_tokens` table + full rotation + reuse-detection + family revocation, present since `20260415184241_InitialCreate`.                                                     |
| 3  | Login is a Server Action, Route Handler, or client fetch | **Server Action** (`loginAction` in `apps/web/lib/actions/auth-actions.ts`).                                                                                                        |
| 4  | JWT signing-key configuration                    | Env var `JWT_SECRET` (HS256). No rotation. No `kid` header.                                                                                                                          |
| 5  | Current cookie `sameSite` / `domain` policy      | `SameSite=Strict`, `HttpOnly`, `Secure (prod)`, `Path=/`, **no `Domain`** → host-only on whichever origin emits the `Set-Cookie`.                                                    |
| 6  | Service Worker / PWA interference                | Custom `sw.js` exists, network-firsts `/api/*`, does not strip credentials. Not the cause.                                                                                           |

---

## 13. Diagnosis — single sentence

**The `refresh_token` cookie is scoped to the Next.js frontend origin, but the browser's silent-refresh call goes cross-origin to the .NET API — the cookie never gets attached, the API returns 401, and the AuthProvider clears the in-memory access token, so the user appears to be logged out on every page reload.**

---

## 14. Where the brief conflicts with reality (decisions needed)

I want to flag these before I start Phase 1, because fixing them naively would either downgrade security or generate churn that doesn't address the bug.

1. **"Refresh token stored as SHA-256 hash."** Current is **BCrypt EnhancedHashPassword(factor 12)**. BCrypt is slower than needed for a random 256-bit secret — HMAC-SHA-256 keyed with `JWT_SECRET` would be faster and equally strong for this purpose. **Recommend: skip for Phase 1, file as a follow-up. The bug isn't in the hash.**

2. **"Refresh tokens stored … linked via `ReplacedByTokenHash`."** Current schema links via **`ReplacedById` (FK to prior row)**, not a hash. Functionally equivalent. **Recommend: keep as-is.**

3. **"Rotation on every use."** Already in place.

4. **"Reuse detection: if a revoked refresh token is presented, revoke entire token family."** Already in place — and it's stronger than family-scoped: it revokes all active tokens for the user. **Recommend: keep.**

5. **"`FamilyId` column."** Not present. Current reuse detection is per-user. Adding `FamilyId` would scope reuse to one device's chain. **Recommend: skip unless you want per-device granularity; flag as follow-up.**

6. **"`SameSite=lax`."** Current is `Strict`. `Strict` is the correct posture here — no cross-site navigation needs to carry this cookie, and the SPA's own calls will carry it fine. **Recommend: keep `Strict`.**

7. **"Tenants on subdomains of `*.educonnect.app` → cookie domain strategy must preserve tenant isolation."** Tenants are not on subdomains today (§9). **Recommend: defer as part of a future "move to subdomain tenants" project. For now, document the JWT-claim model.**

8. **"Login MUST be a Route Handler, not a Server Action, to eliminate the Vercel cookie-commit race."** The Vercel cookie-commit race is **not** what's causing the bug. The bug is origin-scope, not timing. Moving login to a Route Handler does not, on its own, fix it — the cookie still lands on the Next.js origin. The real fix is to **route the browser's silent refresh through a Next.js Route Handler** so the browser call is same-origin to Next.js, and Next.js forwards the cookie to the API server-to-server. **Recommend: leave login as a Server Action; add Route Handler only for `/api/auth/refresh` (and optionally `/api/auth/logout`).**

9. **"`MeQuery` / `/auth/me`."** Not present today — the user is derived client-side from JWT claims (`getUserFromToken`). Useful only if we move to server-side rendering of authenticated pages. **Recommend: skip for Phase 1.**

10. **"One-time client-side cleanup of legacy `localStorage` keys (e.g. `educonnect_token`)."** **No such key is or has ever been written.** This cleanup is a no-op. **Recommend: skip.**

11. **"Index on `TokenHash`, composite `(UserId, FamilyId)`."** `TokenHash` is never queried (lookup is PK). `FamilyId` doesn't exist. **Recommend: skip both.**

12. **"Rate-limit `/auth/refresh` and `/auth/login` per IP."** There's a global per-user-or-IP limiter today. I'll verify in Phase 1 that anonymous IP-keyed limits apply to these paths and add a tighter per-endpoint policy if not.

---

## 15. Proposed minimal Phase 1 fix (for approval)

**Frontend**:

- **New** `apps/web/app/api/auth/refresh/route.ts` — Route Handler. Reads the refresh cookie from Next's own cookie store, forwards it to `<API>/api/auth/refresh` as a `Cookie` header via server-to-server fetch, parses the upstream `Set-Cookie`, re-issues on the Next origin, returns the `{ accessToken, expiresIn, mustChangePassword }` JSON. Single-flight protection stays at the browser layer.
- **Change** `apps/web/lib/api-client.ts:refreshAccessToken` → fetch `/api/auth/refresh` (same-origin to Next.js) instead of `${NEXT_PUBLIC_API_URL}/api/auth/refresh`.
- **Optional** `apps/web/app/api/auth/logout/route.ts` — symmetric Route Handler so the browser can trigger logout same-origin. Current Server Action logout still works, so this is cosmetic.
- **No change** to `tokenStore` / in-memory access token. No change to login Server Action. No change to CSP.

**Backend**:

- **No change required** for the core bug.
- Optional hardening (file as follow-ups unless you want them now): persist UA/IP on `RefreshTokenEntity` for forensics; add `jti` claim; scope `/auth/refresh` rate limit explicitly; add `MeQuery`.

**Tests**:

- Playwright: login → hard refresh → still authenticated (all four roles); wait past 15-min access expiry → next API call silent-refreshes with no redirect; explicit logout → cookies cleared → subsequent refresh bounces to login.
- Backend: rotation handler unit tests already exist; add a test asserting rejection when `refresh_token` cookie is missing on the request.
- Security: one Playwright spec that confirms `document.cookie` does not contain `refresh_token` (HttpOnly) and that `localStorage` / `sessionStorage` have no auth keys.

**Migration strategy**:

- No schema changes.
- Zero disruption to existing sessions: users logged in before the fix have a Next-origin cookie; after the fix, that cookie is exactly what the new Route Handler reads. No re-login required.

---

## 16. Open questions back to you

1. **Do you agree with the minimal-fix approach in §15**, or do you want to proceed with the broader refactor in the brief (migrate login to a Route Handler, add `MeQuery`, etc.)?
2. **Any of the recommendations in §14 you want to reverse** (in particular §14.1 hash migration and §14.6 SameSite)?
3. **Tenant model direction**: stay on single-origin, JWT-claim-based multi-tenancy, or is subdomain-per-tenant on the roadmap? (Affects whether we add a Host-vs-JWT cross-check to the refresh endpoint now.)

I will not write implementation code until you confirm.
