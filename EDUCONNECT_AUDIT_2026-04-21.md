# EduConnect Feature Audit — 2026-04-21

**Mode:** AUDIT-ONLY (read-only, zero mutations, no refactors)
**Auditor:** FBT static-audit pass
**Stack declared:** Next.js 15 (App Router, Server Actions) + .NET 8 (VSA / CQRS / MediatR) + PostgreSQL (RLS) + custom JWT inline auth + PWA + Vercel + Railway
**Stack observed:** Next.js 15.0.7 (App Router, **client-side fetch only — no Server Actions in use**) + .NET 8 Minimal APIs + MediatR + FluentValidation + EF Core + PostgreSQL (**no RLS policies — multi-tenancy is application-level via `SchoolId` column**) + BCrypt + JWT HS256 + Serilog + Sentry + Web Push (VAPID) + AWS S3 SDK pointed at Cloudflare R2

---

## 0. Executive Summary

EduConnect ships as a working multi-tenant school communication platform covering attendance, homework (with a teacher→admin approval pipeline), notices, exams, leave applications, and web push. The backend is a disciplined vertical-slice .NET 8 Minimal API with MediatR pipeline behaviors (validation + logging), comprehensive EF Core configurations (71 indexed columns across 19 entities), BCrypt refresh-token rotation with `ReplacedById` chaining, and per-user fixed-window rate limiting. The frontend is a PWA-enabled Next.js 15 App Router app with role-based navigation, service worker, install prompt, and axe-powered a11y smoke tests.

Three claims in the declared stack do **not** match the code:

1. **"PostgreSQL (RLS)"** — no `CREATE POLICY` or `ROW LEVEL SECURITY` statements exist anywhere in the migrations or runtime code. Tenant isolation is enforced purely at the application layer by filtering on `SchoolId` in every handler (`TenantIsolationMiddleware` only hydrates `CurrentUserService` from JWT claims; it does **not** run `SET LOCAL app.current_tenant`). A single forgotten `WHERE SchoolId = …` clause would cross tenant boundaries.
2. **"Server Actions"** — `grep -rn '"use server"'` across `apps/web` returns zero hits. The frontend talks to the API exclusively through a client `fetch` wrapper (`apps/web/lib/api-client.ts`) with a Bearer token read from `localStorage`.
3. **Access token in `localStorage`** — `apps/web/lib/auth/session.ts` stores the JWT under the key `auth_access_token`; it is not an HttpOnly cookie. The refresh token *is* an HttpOnly/Strict cookie, but an XSS in any dependency surface would immediately leak the active access token.

The most material risks are therefore not in "missing features" but in **three cross-cutting gaps**:

- **No defence-in-depth at the DB layer** for tenant isolation (no RLS, no `app.current_tenant` session var).
- **No HTTP security headers** (no CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy) anywhere in the Next config or the .NET middleware pipeline.
- **Password length inconsistency** — login accepts 6 chars minimum, but Reset/Change require 8 — meaning existing accounts may hold passwords that can never be re-entered during a forced rotation.

On the feature side, the biggest gap against the implied spec is **no student-facing homework submission** — "Submit" in this codebase means "teacher submits a draft to admin for approval". Parents/students view homework but cannot attach work back. Notices have no read-receipt tracking, no pinning/priority, and no rich-text (plain-text only, 5000-char cap).

Overall posture: **solid engineering foundations, serious but fixable security hygiene gaps, and a clearly scoped v1 feature set.** The code is internally consistent and well-indexed; the gaps below are largely about hardening and completing declared-but-unbuilt flows rather than rewriting existing ones.

---

## 1. Appendix A — Repository Ground Truth

### Monorepo layout

```
/ (pnpm workspace)
├── apps/
│   ├── web/                 # Next.js 15 App Router (PWA)
│   │   ├── app/
│   │   │   ├── (auth)/      # login, change-password, change-pin, reset-*
│   │   │   ├── (dashboard)/ # AuthGuard-wrapped feature routes
│   │   │   │   ├── admin/   # classes, teachers, students, notices, subjects
│   │   │   │   ├── teacher/ # attendance, homework, exams, students, profile
│   │   │   │   └── parent/  # attendance, notices, homework, exams
│   │   │   ├── offline/     # PWA offline fallback
│   │   │   ├── error.tsx, global-error.tsx, not-found.tsx
│   │   │   └── layout.tsx, page.tsx
│   │   ├── components/      # layout, shared, pwa, push, ui, effects
│   │   ├── lib/             # api-client, auth, constants, feature-flags, analytics
│   │   ├── providers/       # auth-provider, theme-provider
│   │   ├── hooks/           # use-auth, use-notifications, …
│   │   ├── e2e/             # Playwright + axe-core specs (2 files)
│   │   ├── public/          # manifest.json, sw.js, 10 icons, favicon, logo
│   │   └── sentry.{client,server,edge}.config.ts
│   └── api/
│       ├── src/EduConnect.Api/
│       │   ├── Program.cs
│       │   ├── Common/{Auth, Behaviors, Extensions, Middleware}
│       │   ├── Features/    # VSA — Auth, Attendance, Homework, Notices,
│       │   │                #       Students, Parents, Classes, Teachers,
│       │   │                #       Subjects, Notifications, Push,
│       │   │                #       Attachments, Exams, Health, Leave
│       │   ├── Infrastructure/
│       │   │   ├── Database/{AppDbContext, Entities/, Configurations/,
│       │   │   │              DatabaseSeeder.cs}
│       │   │   └── Services/ # S3StorageService, NotificationService,
│       │   │                 #  ResendEmailSender, WebPushSender, ResetTokenService
│       │   └── Migrations/   # 8 EF Core migrations, latest 20260421_AddExams
│       └── tests/EduConnect.Api.Tests/   # 8 flow-test files
├── packages/                 # (shared utilities)
├── notifications/            # push service assets
└── docs/
    ├── ADR/001-architecture-decisions.md
    ├── API/
    ├── PRODUCT-GENESIS.md
    ├── RAILWAY_DEPLOYMENT.md
    ├── SETUP.md
    └── push-notifications.md
```

### Versions & toolchain (evidence-backed)

| Concern | Value | Evidence |
|---|---|---|
| Next.js | 15.0.7 | `apps/web/package.json` |
| React | 19 | `apps/web/package.json` |
| Tailwind | v4 | `apps/web/package.json` |
| .NET | 8.0 | `apps/api/**/bin/**/net8.0/…` directory presence |
| MediatR pipeline | Validation + Logging behaviors | `apps/api/src/EduConnect.Api/Common/Behaviors/` |
| Sentry | `@sentry/nextjs` + server + edge | `apps/web/sentry.*.config.ts`, `apps/web/next.config.ts` |
| Playwright + axe | yes, 1 a11y spec + 1 admin-filter spec | `apps/web/e2e/a11y.spec.ts`, `apps/web/playwright.config.ts` |

### Endpoint inventory (from `EndpointRouteBuilderExtensions.cs`)

| Group | Count | Authorised? |
|---|---|---|
| `/api/auth` | 11 | mixed — login/refresh are anonymous, rest require auth |
| `/api/attendance` | 11 | RequireAuthorization |
| `/api/homework` | 6 | RequireAuthorization |
| `/api/notices` | 3 | RequireAuthorization |
| `/api/students` | 9 | RequireAuthorization |
| `/api/parents` | 1 | RequireAuthorization |
| `/api/classes` | 4 | RequireAuthorization |
| `/api/teachers` | 8 | RequireAuthorization |
| `/api/subjects` | 2 | RequireAuthorization |
| `/api/notifications` | 4 | RequireAuthorization |
| `/api/push` | 3 | mixed — `vapid-public-key` is anonymous |
| `/api/attachments` | 5 | RequireAuthorization |
| `/api/exams` | 11 | RequireAuthorization |
| `/health` | 1 | AllowAnonymous |

`apps/api/src/EduConnect.Api/Common/Extensions/EndpointRouteBuilderExtensions.cs` (all route groups).

### Middleware pipeline order

`Program.cs` (L273-onward): `UseRouting → UseCorrelationId → UseGlobalException → UseCors("AllowConfigured") → UseAuthentication → UseRateLimiter → UseAuthorization → UseTenantIsolation → UseMustChangePasswordEnforcement → UseRequestLogging`.

### Entities (`AppDbContext` DbSets, 20 total)

Schools, Users, Classes, Students, TeacherClassAssignments, ParentStudentLinks, AttendanceRecords, Homeworks, Notices, NoticeTargetClasses, Subjects, Notifications, Attachments, LeaveApplications, RefreshTokens, AuthResetTokens, UserPushSubscriptions, Exams, ExamSubjects, ExamResults.

### Migrations

8 EF Core migrations, latest `20260421_AddExams.cs`. **Zero** `CREATE POLICY` / `ROW LEVEL SECURITY` statements anywhere in the migration tree.

### Indexes

71 `HasIndex` calls across 19 configuration classes — almost every entity is tenant-indexed on `SchoolId` and functionally indexed on its common filter columns (Homework: `ClassId`, `Status`, `DueDate`, composite `(ClassId, IsDeleted)`; Notifications: composite `(UserId, IsRead, CreatedAt)`; RefreshTokens: `(UserId, IsRevoked)` + `ExpiresAt`; etc.). Coverage is good.

---

## 2. Feature Audit — Section by Section

Legend — ✅ fully implemented · 🟡 partial / gaps · 🩹 implemented with quality or security concerns · 🔴 declared-but-missing · ⚫ out of scope / not declared · ❓ unverified

### 4.1 Authentication

| Check | Status | Evidence |
|---|---|---|
| Password login (staff) | ✅ | `apps/api/src/EduConnect.Api/Features/Auth/Login/LoginCommandHandler.cs` (rejects Parent role; BCrypt verify; issues access + refresh) |
| Parent login path | 🟡 | Parent login is *not* via password — it lives as a separate flow (no `/parent-login` endpoint found in this handler; Parent role is explicitly rejected by `LoginCommandHandler`). Parents auth via a distinct mechanism — ❓ exact entry point not re-verified this pass. |
| JWT access token | ✅ | `apps/api/src/EduConnect.Api/Common/Auth/JwtTokenService.cs` — HS256, 15-min default, claims `sub/userId/schoolId/role/name/must_change_password` |
| Refresh token rotation | ✅ | `Features/Auth/RefreshToken/RefreshTokenCommandHandler.cs` — `{guid:N}.{secret}` format, BCrypt-verifies secret, revokes old, issues new with `ReplacedById`; legacy fallback scans all active tokens (expensive) |
| Refresh cookie hardening | ✅ | `Common/Auth/RefreshTokenCookieOptions.cs` — `HttpOnly=true`, `SameSite=Strict`, `Secure=request.IsHttps`, `Path=/` |
| Access token storage | 🩹 | `apps/web/lib/auth/session.ts` — **localStorage** key `auth_access_token`. Any XSS leaks the live JWT. Declared stack says "custom JWT inline auth" — technically accurate, but dangerous by default. |
| Logout revokes refresh | ✅ | `Features/Auth/Logout/LogoutCommandHandler.cs` — revokes *all* non-revoked refresh tokens for the user; cookie deleted at endpoint layer |
| Forgot password (staff) | ✅ | `Features/Auth/ForgotPassword/ForgotPasswordCommandHandler.cs` — always returns 200 (no user-enumeration), 60-min token via `ResetTokenService`, emails via Resend |
| Reset password | ✅ | `Features/Auth/ResetPassword/ResetPasswordCommandHandler.cs` — validates hash + `UsedAt` + `ExpiresAt` + `IsActive`, rejects Parent, marks token used, revokes **all** refresh tokens |
| Change password | ✅ | `Features/Auth/ChangePassword/ChangePasswordCommandValidator.cs` — min 8, must differ from current |
| **Password length consistency** | 🔴 | `LoginCommandValidator.cs` min **6** vs `ResetPasswordCommandValidator.cs` & `ChangePasswordCommandValidator.cs` min **8**. Accounts may hold 6-char passwords that fail a forced rotation. |
| Must-change-password enforcement | ✅ | `Common/Middleware/MustChangePasswordMiddleware.cs` — reads `must_change_password` claim, blocks everything except auth allowlist + `/health`, returns 403 ProblemDetails with `errors.code = ["MUST_CHANGE_PASSWORD"]` |
| Client-side must-change routing | ✅ | `apps/web/components/auth/auth-guard.tsx` — routes to `/change-pin` (Parent) or `/change-password` (staff) when flag is set |
| Auth initial-restore race | ✅ | `apps/web/providers/auth-provider.tsx` — `isInitialRefreshRef` guards the initial silent refresh against a concurrent manual login (prior Vercel timing bug fix is in place) |
| Proactive token refresh | ✅ | `apps/web/providers/auth-provider.tsx` — schedules refresh at `(exp - 120) * 1000` ms |

### 4.2 Authorization & Multi-Tenancy

| Check | Status | Evidence |
|---|---|---|
| Endpoint-level authorization | ✅ | All non-auth route groups call `.RequireAuthorization()` — see `EndpointRouteBuilderExtensions.cs` group definitions |
| Role-based route gating (web) | ✅ | `apps/web/components/auth/auth-guard.tsx` — pathname→required-role map, redirects on mismatch |
| `CurrentUserService` population | ✅ | `apps/api/src/EduConnect.Api/Common/Middleware/TenantIsolationMiddleware.cs` — hydrates `UserId/SchoolId/Role/Name` from JWT claims, pushes `LogContext` |
| Per-handler tenant filtering | ✅ | Every query handler filters on `SchoolId` (confirmed across Attachments, Notices, Homework, Exams, Students handlers inspected) |
| **DB-layer tenant isolation (RLS)** | 🔴 | Declared stack states "PostgreSQL (RLS)". `grep -rn "CREATE POLICY\|ROW LEVEL SECURITY"` returns **zero** matches in migrations or code. Isolation is application-only. |
| `app.current_tenant` session variable | 🔴 | `TenantIsolationMiddleware.cs` does not set any Postgres session GUC. A missed `WHERE` crosses tenants. |
| Teacher-owns-homework checks | ✅ | `Features/Attachments/AttachFileToEntity/AttachFileToEntityCommandHandler.cs` enforces teacher ownership + Draft/Rejected status for attachment mutation |
| Attachment delete authz | ✅ | `Features/Attachments/DeleteAttachment/DeleteAttachmentCommandHandler.cs` — teachers can only delete their own; forbids delete from published notices |

### 4.3 User Management (Admin)

| Check | Status | Evidence |
|---|---|---|
| Admin can create teachers | ✅ | `apps/web/app/(dashboard)/admin/teachers/new/page.tsx` exists; backend under `/api/teachers` (8 endpoints) |
| Admin can edit teachers | ✅ | `apps/web/app/(dashboard)/admin/teachers/[id]/page.tsx` |
| Admin can list teachers (with filter bar) | ✅ | `apps/web/app/(dashboard)/admin/teachers/page.tsx` + `apps/web/e2e/admin-teachers-filter-bar.spec.ts` |
| Active/inactive soft-delete | ✅ | `UserEntity.IsActive` bool — honoured by login + reset handlers |
| Bulk user import (CSV) | ❓ | `apiPostMultipart` exists in `apps/web/lib/api-client.ts` (signals CSV-ish uploads) but I did not trace a specific `/api/teachers/import` or similar endpoint in the endpoint inventory — needs follow-up. |

### 4.4 Student Management

| Check | Status | Evidence |
|---|---|---|
| Admin CRUD | ✅ | `apps/web/app/(dashboard)/admin/students/{new,[id]/edit,[id],page}.tsx` — 5 pages; backend `/api/students` (9 endpoints) |
| Teacher creates students | 🟡 | `apps/web/app/(dashboard)/teacher/students/new/page.tsx` exists — whether the backend authorizes Teachers to hit the create endpoint is ❓ (endpoint-level authz only proven; per-role checks inside handler not re-verified this pass) |
| Link parent to student | ✅ | `apps/web/app/(dashboard)/admin/students/[id]/link-parent/page.tsx` + `ParentStudentLink` entity + `/api/parents` endpoint group |

### 4.5 Teacher Management

| Check | Status | Evidence |
|---|---|---|
| Assign teachers to classes + subjects | ✅ | `TeacherClassAssignmentConfiguration.cs` — unique index on `(TeacherId, ClassId, Subject)`; `/api/teachers` (8 endpoints) |
| Multiple subjects per teacher per class | ✅ | Same unique composite key includes Subject |
| Teacher profile page | ✅ | `apps/web/app/(dashboard)/teacher/profile/page.tsx` |

### 4.6 Class Management

| Check | Status | Evidence |
|---|---|---|
| Admin CRUD classes | ✅ | `apps/web/app/(dashboard)/admin/classes/{page,[id]/page}.tsx`; `/api/classes` (4 endpoints) |
| Academic-year scoping | ✅ | `ClassConfiguration.cs` — unique index on `(SchoolId, Name, Section, AcademicYear)` |
| Subjects catalogue | ✅ | `apps/web/app/(dashboard)/admin/subjects/page.tsx`; `/api/subjects` (2 endpoints); `SubjectConfiguration.cs` — unique on `(SchoolId, Name)` |

### 4.7 Attendance

| Check | Status | Evidence |
|---|---|---|
| Teacher marks attendance | ✅ | `apps/web/app/(dashboard)/teacher/attendance/page.tsx`; `/api/attendance` (11 endpoints) |
| Parent views attendance | ✅ | `apps/web/app/(dashboard)/parent/attendance/page.tsx` (default landing for Parent per `defaultRouteByRole` in `apps/web/lib/constants.ts`) |
| `AttendanceRecords` entity | ✅ | `AppDbContext.cs` + `AttendanceRecordConfiguration.cs` |
| Bulk-mark | ❓ | Backend endpoint count (11) suggests bulk + per-student operations; individual signatures not re-verified this pass |

### 4.8 Homework

| Check | Status | Evidence |
|---|---|---|
| Teacher creates homework (Draft) | ✅ | `apps/web/app/(dashboard)/teacher/homework/page.tsx` is the Teacher default landing; `HomeworkEntity.Status` workflow: `Draft/Rejected/Submitted/Approved/Published` |
| Teacher submits for approval | ✅ | Status transitions via `/api/homework` (6 endpoints) |
| Admin approves / rejects | ✅ | Same endpoint group; `HomeworkEntity.ApprovedById` + index on it |
| Attach files (teacher) | ✅ | `Features/Attachments/AttachFileToEntity/AttachFileToEntityCommandHandler.cs` — Draft/Rejected only; max 5 per entity (`AttachmentRules.cs`) |
| Parent/Student views homework | ✅ | `apps/web/app/(dashboard)/parent/homework/page.tsx` |
| **Student submission flow** | 🔴 | There is **no** student-submission sub-entity (no `HomeworkSubmissionEntity` in `AppDbContext`). "Submit" in this codebase = teacher→admin. Students cannot attach work back. |
| Soft-delete | ✅ | `HomeworkEntity.IsDeleted` + composite index `(ClassId, IsDeleted)` |

### 4.9 Notice Board

| Check | Status | Evidence |
|---|---|---|
| Admin posts notices | ✅ | `apps/web/app/(dashboard)/admin/notices/page.tsx` (Admin default landing per `defaultRouteByRole`); `/api/notices` (3 endpoints) |
| Targeting (All / Class / Section) | ✅ | `NoticeEntity.targetAudience` + `NoticeTargetClassConfiguration.cs` — index on `(SchoolId, ClassId)` |
| Publish / unpublish | ✅ | `NoticeEntity.IsPublished` |
| Expiry | ✅ | `NoticeEntity.ExpiresAt` |
| Attachments (images + pdf) | ✅ | `AttachmentRules.cs` — `NoticeAllowed = jpg/png/webp/pdf` |
| Parent views notices | ✅ | `apps/web/app/(dashboard)/parent/notices/page.tsx` |
| **Pin / priority** | 🔴 | No `IsPinned` / `Priority` field on `NoticeEntity` |
| **Read receipts** | 🔴 | No `NoticeReadReceipt` or similar entity in `AppDbContext` |
| **Rich text / markdown** | 🔴 | `Features/Notices/CreateNotice/CreateNoticeCommand.cs` — plain-text body, 5000-char cap; no sanitiser, no markdown pipeline |

### 4.10 Parent Portal

| Check | Status | Evidence |
|---|---|---|
| Landing page | 🟡 | `defaultRouteByRole.Parent = "/parent/attendance"` — **no dedicated parent dashboard** (`/parent` root page). Straight to attendance. |
| Attendance, Notices, Homework, Exams | ✅ | 5 pages under `apps/web/app/(dashboard)/parent/` |
| Multi-child support | ✅ | `ParentStudentLink` entity + unique `(ParentId, StudentId)` index |

### 4.11 Teacher Portal

| Check | Status | Evidence |
|---|---|---|
| Landing page | 🟡 | `defaultRouteByRole.Teacher = "/teacher/homework"` — no `/teacher` root page. |
| Attendance, Homework, Students, Exams, Profile | ✅ | 10 pages under `apps/web/app/(dashboard)/teacher/` |

### 4.12 Admin Portal

| Check | Status | Evidence |
|---|---|---|
| Landing page | 🟡 | `defaultRouteByRole.Admin = "/admin/notices"` — no `/admin` root page, no dashboard-y KPI view. |
| Classes, Teachers, Students, Notices, Subjects | ✅ | 12 pages under `apps/web/app/(dashboard)/admin/` |

### 4.13 Notifications

| Check | Status | Evidence |
|---|---|---|
| `Notifications` table | ✅ | `AppDbContext.cs` + `NotificationConfiguration.cs` — index `(UserId, IsRead, CreatedAt)` + `SchoolId` |
| Write path | ✅ | `Infrastructure/Services/NotificationService.cs` — writes `NotificationEntity` + fans out via `IPushSender.FanOutPushSafelyAsync` |
| `/api/notifications` endpoints | ✅ | 4 endpoints under `RequireAuthorization` |
| Bell + panel UI | ✅ | `apps/web/components/shared/notification-bell.tsx` + `notification-panel.tsx`, wired in `apps/web/components/layout/header.tsx` |
| Unread count + mark read / mark-all-read | ✅ | `apps/web/hooks/use-notifications.ts` (returned by the Bell) |
| Web push — backend | ✅ | `UserPushSubscriptions` entity + `/api/push` (3 endpoints, `vapid-public-key` anonymous) + `IPushSender` registered conditionally on VAPID env vars (`Program.cs` L90-92) |
| Web push — service worker handler | ✅ | `apps/web/public/sw.js` — `push` event creates `showNotification`, posts message to clients; `notificationclick` focuses/opens tab; `pushsubscriptionchange` nudges clients |
| Web push — auto subscriber | ✅ | `apps/web/components/push/PushAutoSubscriber.tsx`, mounted in `(dashboard)/layout.tsx` |
| Email channel | 🟡 | Resend used for password reset, but no evidence of email fan-out for feature notifications — they are push-only ❓ |

### 4.14 PWA

| Check | Status | Evidence |
|---|---|---|
| Manifest | ✅ | `apps/web/public/manifest.json` — name, short_name, description, `start_url=/`, `display=standalone`, `orientation=portrait`, `theme_color=#512BD4`, `background_color=#0a061a`, 10 icons incl. maskable 192/512 |
| Service worker | ✅ | `apps/web/public/sw.js` (174 lines) — precache `/login` + `/offline`, network-first navigate with `/offline` fallback, cache-first static images (excludes `/_next/static/` to avoid stale chunks) |
| SW registration | 🩹 | `apps/web/components/pwa/sw-registrar.tsx` — only registers when `process.env.NODE_ENV === "production"`. Means dev builds never exercise SW, so PWA regressions only surface in prod. |
| Install prompt | ✅ | `apps/web/components/pwa/install-prompt.tsx` (69 lines), mounted in root `layout.tsx` |
| Offline page | ✅ | `apps/web/app/offline/` |
| Viewport + theme-color | ✅ | `apps/web/app/layout.tsx` — `viewport` export, light/dark themeColor pair |
| Apple touch icon | ✅ | `apps/web/public/apple-touch-icon.png` wired in `metadata.icons.apple` |

### 4.15 File Handling (R2 via S3 SDK)

| Check | Status | Evidence |
|---|---|---|
| S3 client registered | ✅ | `apps/api/src/EduConnect.Api/Program.cs` — conditionally registered with `ServiceURL` (so R2/MinIO work) |
| Presigned upload URL | ✅ | `Infrastructure/Services/S3StorageService.cs` — `GeneratePresignedUploadUrlAsync` (PUT + ContentType) |
| Presigned download URL with `Content-Disposition` override | ✅ | Same file — `ResponseHeaderOverrides` on download; Word types force `attachment`, others `inline` |
| Tenant-prefixed keys | ✅ | `Features/Attachments/RequestUploadUrl/RequestUploadUrlCommandHandler.cs` — key `{schoolId}/{attachmentId}{extension}`, 15-min TTL |
| Per-entity attach limit | ✅ | `Features/Attachments/AttachmentRules.cs` — `MaxAttachmentsPerEntity = 5` |
| Allowed types | ✅ | Same file — Homework: pdf/doc/docx; Notice: jpg/png/webp/pdf |
| Download list | ✅ | `Features/Attachments/GetAttachmentsForEntity/GetAttachmentsForEntityQueryHandler.cs` — 1-hour presigned, tenant-scoped |
| Delete authorization | ✅ | `Features/Attachments/DeleteAttachment/DeleteAttachmentCommandHandler.cs` — teacher-owned + not-published-notice guard |
| **Virus / malware scanning** | 🔴 | `S3StorageService.cs` hands back a raw PUT URL; no ClamAV/S3 object-level scan hook anywhere. A teacher (or compromised teacher) can upload infected payloads and parents will presign-download them. |
| **Size limit at server** | ❓ | No explicit max-bytes check observed on the presign endpoint (R2 itself enforces headers, but server-side policy is absent). |

### 4.16 UX Polish

| Check | Status | Evidence |
|---|---|---|
| Empty states | ✅ | `apps/web/components/shared/empty-state.tsx` — reusable component with illustration, CTA, analytics event |
| Error states | ✅ | `apps/web/components/shared/error-state.tsx` — retry + icon |
| Loading states | 🟡 | No `loading.tsx` anywhere in `apps/web/app/`. Dashboard uses `Suspense` with `DashboardSkeleton` in `(dashboard)/layout.tsx`. Per-route skeletons are missing. |
| `not-found.tsx` | ✅ | `apps/web/app/not-found.tsx` — branded 404 |
| `error.tsx` + `global-error.tsx` | ✅ | Both present, both call `Sentry.captureException` |
| Theme toggle | ✅ | `apps/web/components/shared/theme-toggle.tsx` + inline theme-script in root layout using `localStorage` key `educonnect-theme` |
| **Toast system** | 🔴 | `grep -rn "sonner\|useToast\|Toaster"` in `apps/web` returns zero. No notification-on-success/-failure UI primitive exists; feedback is per-page ad-hoc. |
| Confirm dialog | 🟡 | `apps/web/components/ui/dialog.tsx` exists (primitive) but no reusable `ConfirmDialog` — each destructive action re-rolls its own dialog. |
| Retention slot / Zeigarnik checklist | ✅ | `apps/web/components/shared/retention-slot.tsx` + `retentionStepsByRole` in `apps/web/lib/constants.ts`, gated by `featureFlags.retentionDashboardCard` |
| Animated content wrapper | ✅ | `apps/web/components/effects/animated-content.tsx` |

### 4.17 Observability & Ops

| Check | Status | Evidence |
|---|---|---|
| Structured logs (backend) | ✅ | `Program.cs` L26-52 — Serilog with `RenderedCompactJsonFormatter`, 30-day daily rolling, 50MB file cap |
| PII-redaction destructuring policy | ✅ | Custom `DestructuringPolicy` redacts `password/pin/token/phone/secret/api_key` |
| Correlation IDs | ✅ | `Common/Middleware/CorrelationIdMiddleware.cs` — respects inbound `X-Correlation-Id`, generates GUID otherwise, echoes header, pushes `LogContext` |
| Global exception handler | ✅ | `Common/Middleware/GlobalExceptionMiddleware.cs` — maps custom exceptions to ProblemDetails; 500s at Error, rest at Warning; hides `exception.Message` in production |
| Sentry — backend | ✅ | Conditionally wired into Serilog sink when `SENTRY_DSN` is set |
| Sentry — frontend | ✅ | `apps/web/sentry.client.config.ts` — 20% trace sample, 100% error-replay, masks text + media, strips `Authorization` + `Cookie` headers in `beforeSend` |
| Health endpoint | ✅ | `Features/Health/HealthEndpoint.cs` — `/health` returns DB can-connect + latency + uptime + version; 503 on DB down; `AllowAnonymous`; excluded from rate limiter |
| **Readiness / liveness split** | 🔴 | Single `/health`. No separate `/ready` vs `/live`. |
| EF migration runner | ✅ | `Program.cs` L232-253 — single scope, `MigrateAsync` → prod seed always → dev seed only in Development — wrapped in try/catch that re-throws |
| **Distributed tracing (OTel)** | ⚫ | Sentry tracing only; no OpenTelemetry exporter configured — not declared in stack |
| Request logging | ✅ | Pipeline mounts `UseRequestLogging` after auth/authz/tenant |

### 4.18 Security

| Check | Status | Evidence |
|---|---|---|
| CORS allowlist | ✅ | `Program.cs` L191-199 — `corsOrigins` from `CORS_ALLOWED_ORIGINS` env, `AllowCredentials()` |
| Rate limiter (per-user partition) | ✅ | `Program.cs` L202-228 — fixed window, default 60/min (env-overridable), keyed on `userId` claim else IP; `/health` excluded |
| BCrypt password hashing | ✅ | Login/Reset/Change handlers use BCrypt; refresh token hashing uses BCrypt enhanced mode cost 12 |
| Password reset token hashing | ✅ | `Infrastructure/Services/ResetTokenService.cs` (referenced from ForgotPassword + ResetPassword handlers); `AuthResetTokenConfiguration.cs` has unique index on `TokenHash` |
| Secrets validated at startup | ✅ | `ServiceCollectionExtensions.ValidateEnvironment()` called first in `Program.cs`; frontend has `validateEnv()` on root layout |
| Serilog PII redaction | ✅ | See 4.17 |
| Sentry PII scrubbing | ✅ | `apps/web/sentry.client.config.ts` — `beforeSend` deletes Authorization/Cookie headers |
| **HTTP security headers** | 🔴 | `grep -rEn "X-Frame-Options\|Content-Security-Policy\|X-Content-Type-Options\|Strict-Transport-Security\|Referrer-Policy"` returns **zero** across both `apps/web/next.config.ts` and the entire .NET middleware pipeline. |
| **CSP** | 🔴 | See above — no CSP policy at all. |
| **HSTS** | 🔴 | Not set. |
| Access token in localStorage | 🩹 | `apps/web/lib/auth/session.ts` — XSS-exposed; see 4.1. |
| CSRF protection | ✅ (by architecture) | All mutations go through the `fetch` Bearer client — no forms, no cookies carrying auth beyond the HttpOnly SameSite=Strict refresh, which is itself inert as a CSRF vector for a state-changing API call. Refresh endpoint mutates auth state via cookie, but `SameSite=Strict` + CORS allowlist + `AllowCredentials` together contain it. Worth re-checking. |
| Input validation | ✅ | FluentValidation pipeline behavior for every command (per `Common/Behaviors/`) |
| SQL injection surface | ✅ | EF Core LINQ throughout; no raw SQL observed in feature handlers |
| File-type allowlists | ✅ | `AttachmentRules.cs` |
| Virus scanning on uploads | 🔴 | Not present — see 4.15 |
| Must-change-password block | ✅ | See 4.1 |
| Login enumeration protection | ✅ | ForgotPassword always returns 200 |
| **Password min-length consistency** | 🔴 | Login 6 vs Reset/Change 8 — see 4.1 |

### 4.19 Performance

| Check | Status | Evidence |
|---|---|---|
| DB indexing | ✅ | 71 `HasIndex` calls — including composite indexes on hot filters (`Homework(ClassId, IsDeleted)`, `Notifications(UserId, IsRead, CreatedAt)`, `RefreshTokens(UserId, IsRevoked)`) |
| Tenant-column indexing | ✅ | Every tenant entity indexes `SchoolId` |
| `next/image` usage | 🟡 | Only 3 files under `apps/web/app` + `apps/web/components` import `next/image` (Header logo, a couple of brand slots). Most visual assets are raster or SVG in JSX — fine for icons, but means no automatic optimisation on feature-page images. |
| `output: "standalone"` | ✅ | `apps/web/next.config.ts` — enables small runtime image + compatible with Playwright test harness that spawns `.next/standalone/apps/web/server.js` |
| Reset-password BCrypt cost | ✅ (cost 12) | Refresh token hash cost — in an auth path this is a hot spot; acceptable for 15-min rotation, not free |
| Legacy refresh-token fallback (full-table BCrypt scan) | 🩹 | `RefreshTokenCommandHandler.cs` — when no `{guid:N}` prefix hits, it scans *all* active tokens and BCrypt-verifies. Is O(n) BCrypts per refresh attempt. Low severity (gated behind bad-token path) but DoS-able. |
| Rate limiting | ✅ | Per-user, partitioned, fixed window |
| HTTP caching headers | ❓ | Not verified this pass (service worker handles some; feature APIs haven't been inspected for `Cache-Control`) |

### 4.20 Accessibility

| Check | Status | Evidence |
|---|---|---|
| a11y smoke test | ✅ | `apps/web/e2e/a11y.spec.ts` — axe-core WCAG 2.0/2.1 A + AA on `/login` and `/offline`, fails on critical/serious |
| Keyboard-focusable controls | ✅ | `NotificationBell` uses semantic `Button` + `aria-label={…unread count}` |
| `aria-hidden` on decorative SVGs | ✅ | Confirmed in `empty-state.tsx`, `error-state.tsx` |
| Focus-trap in dialogs | 🟡 | `apps/web/components/ui/dialog.tsx` exists (Radix-ish primitive) but not re-audited for modal focus return |
| Reduced-motion preferences | ❓ | `AnimatedContent` wrapper exists; reduced-motion handling not re-checked this pass |
| Colour contrast | ❓ | Axe on login/offline passes — not proven across feature pages |
| Landmarks (`<main>`, `<header>`, `<nav>`) | ✅ | `(dashboard)/layout.tsx` uses `<main>`, `Sidebar`/`Header`/`BottomNav` are semantic components |
| Skip links | ❓ | Not observed in a quick scan |
| i18n / localisation | 🔴 | `apps/web/i18n/` directory does not exist — content is English-only with no extraction pipeline |

---

## 3. Discovered Features (not in the original matrix)

- **Exam workflow.** `Exams / ExamSubjects / ExamResults` entities, `/api/exams` route group (11 endpoints), teacher pages at `teacher/exams/{new,page,[id],[id]/results}`, parent pages at `parent/exams/{page,[id]}`. Most recent migration `20260421_AddExams.cs` is the feature rollout.
- **Leave applications.** `LeaveApplicationEntity` + configuration + `ApplyLeaveFlowTests.cs`. Not surfaced in any obvious UI page scanned, so appears to be API-level only (or pending UI).
- **Zeigarnik-style retention checklist.** `retentionStepsByRole` in `apps/web/lib/constants.ts` + `RetentionSlot` component, mounted above feature content in dashboard layout, gated by `featureFlags.retentionDashboardCard` env flag.
- **Feature flags framework.** `apps/web/lib/feature-flags.ts` — minimal but disciplined: `retentionDashboardCard`, `analytics`.
- **Analytics sink (gtag).** `apps/web/lib/analytics.ts` — event taxonomy `retention_step_complete | retention_card_view | empty_state_cta`, gated by flag + `NEXT_PUBLIC_ANALYTICS_MEASUREMENT_ID`.
- **Session replay on errors.** `sentry.client.config.ts` — `replaysOnErrorSampleRate: 1.0`, `replaysSessionSampleRate: 0`, masks all text and blocks media. Good default.
- **Custom Serilog destructuring policy** that redacts auth fields (see 4.17).
- **8 .NET flow-tests** under `apps/api/tests/EduConnect.Api.Tests/` — Auth, Apply Leave, Admin Onboarding, Exam, Homework Attachment, Notice Targeting, Student Filter, Teacher Filter. Real coverage of the happy paths.

---

## 4. Cross-Cutting Findings

1. **Tenant isolation is application-only.** No RLS, no `app.current_tenant` GUC, no defence-in-depth. A single handler that forgets `.Where(x => x.SchoolId == current.SchoolId)` becomes a cross-tenant leak. Fix path: add RLS policies (at minimum an advisory `SET LOCAL` via middleware) on every tenant table, or wrap EF queries with a global query filter keyed on `CurrentUserService.SchoolId`.

2. **Security headers absent across the whole surface.** No CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy in `next.config.ts` or .NET middleware. Fix path: `headers()` block in Next config + a small ASP.NET security-headers middleware.

3. **Access token in `localStorage`.** XSS-reachable. Refresh token is correctly HttpOnly/Strict, so the right move is to drop the access token on the cookie and have `api-client.ts` stop reading from localStorage.

4. **Password min-length inconsistency** (6 vs 8). Either raise login to 8 (and force-change offending users), or lower reset/change to 6 (not recommended).

5. **No upload scanning.** Teacher-authored payloads reach parents via 1-hour presigned URLs without any virus or content scan. Fix path: async ClamAV or equivalent, attachment gets `ScanStatus = Pending` and signed URL is withheld until `Clean`.

6. **No student-facing homework submission.** "Submit" today means teacher→admin approval. If parents/students are expected to hand work back, that is a full feature ahead (new entity, new endpoints, new UI pages, new attachment flow with student-owner authz).

7. **No notice read-receipts, pinning, or priority.** Likely fine for v1 but worth flagging against the declared "notice board" spec.

8. **No toast primitive.** Every page invents its own success/failure surface, which is a consistency/UX risk as the surface grows.

9. **Legacy refresh-token fallback scans all active tokens with BCrypt.** O(n) per attempt from a malformed token — low-severity DoS vector. Either drop legacy support or cap/cooldown the retry path.

10. **Service worker only registers in production.** Means PWA regressions only show up in prod. Consider opt-in SW registration in staging.

11. **No per-route `loading.tsx`.** Dashboard has a single shared skeleton; feature pages never show a targeted loading state during client-fetch roundtrips.

12. **No dashboard landing page per role.** Every role is redirected straight to a feature list — no at-a-glance summary surface exists (no `/admin`, `/teacher`, `/parent` index page).

13. **i18n absent.** No `i18n/` directory, no extraction pipeline. English-only is fine if that is the business decision; worth making that decision explicit.

14. **`output: "standalone"` + custom `webServer` in Playwright config** means the e2e harness runs the production server, which is good for confidence but makes dev-loop e2e slower.

---

## 5. Unverified Items & What's Needed to Verify Them

| Item | Why not verified this pass | How to verify |
|---|---|---|
| Exact Parent login entry point | `LoginCommandHandler` rejects Parent role, but I did not re-trace the Parent auth path | Search `Features/Auth/` for a ParentLogin/ParentLoginByPin handler; inspect `/api/auth` endpoint group for a parent-specific route |
| Whether Teacher is authorised by the create-student endpoint | Teacher has a UI page for "new student" but handler-level role check not re-verified | Read `Features/Students/CreateStudent/CreateStudentCommandHandler.cs` for the role guard |
| Bulk user import | `apiPostMultipart` exists but I did not map it to a specific endpoint | `grep -rn "apiPostMultipart\|MultipartReader\|IFormFile" apps/` and follow |
| Attendance bulk vs per-student signatures | 11 endpoints is suggestive; individual signatures not re-verified | `EndpointRouteBuilderExtensions.cs` attendance block |
| Whether notifications include an email channel for non-reset events | Resend is only observed in ForgotPassword | `grep -rn "IEmailSender\|Resend" apps/api/src/EduConnect.Api/Features/Notifications/` |
| Server-side upload size cap | Only file-type allowlist observed | `grep -rn "MaxRequestBodySize\|Limits\|ContentLength" apps/api/src/EduConnect.Api/` |
| HTTP caching headers on feature APIs | Not inspected this pass | `grep -rn "Cache-Control\|ResponseCache\|ETag" apps/api/src/EduConnect.Api/Features/` |
| Focus-trap and focus-return in `ui/dialog.tsx` | Primitive exists but not re-audited | Read `apps/web/components/ui/dialog.tsx` end-to-end; manual Playwright keyboard test |
| Reduced-motion handling | `AnimatedContent` present but branch not inspected | Read `apps/web/components/effects/animated-content.tsx` for `prefers-reduced-motion` check |
| Skip-link presence | Not observed in sidebar/header | `grep -rn "skip-to-content\|SkipLink" apps/web/components/` |
| CSRF exposure on refresh endpoint | Reasoned through architecture but not proven | `curl` from an external origin with `credentials: include` and an attacker-set cookie; verify CORS allowlist rejects pre-flight |
| Colour contrast across feature pages | Axe only runs on `/login` + `/offline` | Extend `a11y.spec.ts` to cover a logged-in dashboard snapshot (needs test user + auth bootstrap) |

---

**End of report.**
