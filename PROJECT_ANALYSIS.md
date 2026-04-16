# EduConnect — Full Project Analysis
**Generated:** April 13, 2026  
**Analysed by:** Claude (Cowork)  
**Project Owner:** Future Beyond Technology (FBT) / FIROSE Enterprises, Chennai, India

---

## 1. Project Overview

EduConnect is a **school communication platform** designed to replace WhatsApp groups, paper circulars, and verbal messages with a single trusted digital system. Its three core domains are **attendance tracking, homework management, and school notices** — served to three distinct user roles: Parents, Teachers, and Admins.

The product is built and maintained by a **solo developer** targeting a **6–8 week MVP timeline**, with Railway as the cloud host.

---

## 2. Repository Structure

The project is a **pnpm monorepo** orchestrated by **Turborepo**, containing two deployable applications and three shared packages:

```
educonnect/
├── apps/
│   ├── web/          → Next.js 15 frontend (PPR)
│   └── api/          → .NET 8 Minimal API (VSA + CQRS)
├── packages/
│   ├── api-client/   → Generated TypeScript API client
│   ├── ui/           → Shared design tokens (colors, spacing, typography)
│   └── config/       → Shared ESLint, TypeScript, and Tailwind configs
├── docs/
│   ├── ADR/          → Architecture Decision Records
│   ├── PRODUCT-GENESIS.md
│   └── SETUP.md
├── scripts/          → Local dev and DB management scripts (bash + PowerShell)
├── docker-compose.yml
├── turbo.json
└── package.json
```

**Package manager:** pnpm 9.15.0 | **Node requirement:** ≥ 20.0.0

---

## 3. Technology Stack

### Backend — `apps/api`
| Concern | Technology |
|---|---|
| Runtime | .NET 8 (net8.0) |
| API style | Minimal API (no controllers) |
| Architecture | Vertical Slice Architecture + CQRS |
| Mediator | MediatR 12.4 |
| Validation | FluentValidation 11.10 |
| ORM | Entity Framework Core 8 + Npgsql |
| Database | PostgreSQL 16 (Railway) |
| Auth | Custom JWT (System.IdentityModel.Tokens.Jwt 8.0) |
| Password hashing | BCrypt.Net-Next |
| Logging | Serilog (console + rolling JSON file + Sentry sink) |
| Error tracking | Sentry.AspNetCore 4.12 |
| Email | Resend (via HTTP client — `ResendEmailService`) |
| File storage | AWS S3 SDK — compatible with S3 / Cloudflare R2 / MinIO |
| Rate limiting | ASP.NET Core built-in `FixedWindowRateLimiter` |

### Frontend — `apps/web`
| Concern | Technology |
|---|---|
| Framework | Next.js 15.0.7 |
| React | React 19.0.0 |
| Rendering | PPR (Partial Prerendering) |
| Styling | Tailwind CSS v4 |
| Animations | Framer Motion 11 |
| Icons | Lucide React |
| Error tracking | Sentry/Next.js 8.40 |
| PWA | Custom service worker (`public/sw.js`) + Web Manifest |
| TypeScript | Strict mode, `noUncheckedIndexedAccess`, `noUnusedLocals` |

---

## 4. Architecture Decisions (ADR-001, April 2026)

Seven foundational decisions are locked in and documented:

**ADR-01 — Modular Monolith:** Single deployable unit with clean bounded contexts (Attendance, Homework, Notices). Chosen for solo-developer velocity while preserving future extraction paths.

**ADR-02 — VSA + CQRS + MediatR:** Each feature folder owns its handler, validator, request/response, and endpoint — no cross-cutting layered ceremony. Commands (writes) and queries (reads) are separated within each slice.

**ADR-03 — PostgreSQL on Railway (Very High Reversal Cost):** ACID guarantees for immutable audit records; deeply relational data model. One-way door — changing this post-launch is a quarter-long project.

**ADR-04 — Row-level multi-tenancy via `school_id` (Very High Reversal Cost):** Every table carries a `school_id` FK. EF Core global query filters enforce tenant isolation at the ORM layer. `TenantIsolationMiddleware` enforces it at the HTTP layer.

**ADR-05 — Custom JWT (High Reversal Cost):** Parents authenticate with Phone + PIN (4–6 digits). Teachers/Admins authenticate with Phone + Password (BCrypt). Standard auth providers don't support this dual-mode flow. Access tokens ≤ 15 min (in-memory only). Refresh tokens in HttpOnly Secure cookies with rotation.

**ADR-06 — PPR (Partial Prerendering):** Static shell renders instantly (target FCP < 1.5s); dynamic islands hydrate with authenticated data. The static shell eliminates CLS.

**ADR-07 — Extend-only API versioning:** No URL versioning for MVP. Breaking changes trigger `/v1/` prefix. Both client and server are controlled.

---

## 5. Database Schema

All tables carry `school_id` for row-level tenant isolation. All timestamps use `TIMESTAMPTZ`. Critical data uses soft deletes (`is_deleted`, `deleted_at`).

### Core Tables

**`schools`** — Tenant root. Each school has a unique `code`, contact details, and `is_active` flag.

**`users`** — Single table for all user roles (`Parent`, `Teacher`, `Admin`). Scoped to `school_id`. Phone is unique per school. `password_hash` is NULL for parents (PIN-only); `pin_hash` is NULL for staff (password-only).

**`classes`** — School classes with `name`, `section`, and `academic_year`. Unique per school per year.

**`students`** — Linked to a class and school. Each student has a `roll_number` unique within their class.

**`parent_student_links`** — Many-to-many junction between parents (users) and students. Immutable record — no soft delete.

**`teacher_class_assignments`** — Many-to-many between teachers and classes, scoped to a `subject`. Includes an `assigned_by` FK (added in migration 007).

**`attendance_records`** — Append-only with soft delete. One record per student per day (partial unique index on `is_deleted = FALSE`). Status constrained to `Absent` or `Late`. `entered_by_role` constrained to `Parent` or `Admin`.

**`homework`** — Class-scoped, subject-tagged, with `due_date`, `is_editable` flag, and soft delete.

**`notices`** — School-wide or class-targeted announcements. Publish/expire lifecycle with `target_audience` constrained to `All`, `Class`, or `Section`.

**`subjects`** — School-specific subject catalogue. Unique per school by name. Added in migration 007.

**`notifications`** — In-app notification feed per user. Types: `notice_published`, `homework_assigned`, `absence_marked`. Links back to source entity via `entity_id` + `entity_type`.

**`attachments`** — S3-backed file metadata. Attached to `homework` or `notice` entities. Constrained to JPEG/PNG/WebP/PDF, max 10 MB. Orphan-detection index on `uploaded_at WHERE entity_id IS NULL`.

**`refresh_tokens`** — Rotation chain stored as hashed tokens. `replaced_by_id` self-reference tracks the chain.

**`auth_reset_tokens`** — Used by forgot-password and forgot-PIN flows (added in migration 004).

### Migration Sequence
```
001 — Foundation tables (all core entities)
003 — Remove OTP, add PIN hash to users
004 — Add email field to users + auth_reset_tokens table
006 — Student management columns (is_active, date_of_birth, etc.)
007 — subjects table + assigned_by on teacher_class_assignments
008 — notifications table
009 — attachments table
```
Seed migrations (development-only): initial school/users/students data, corrected PIN/password hashes, subject seeds.

---

## 6. API Surface

**Base path:** `/api/`  
**Auth:** JWT Bearer (all endpoints except auth group which mixes anonymous + authorized)  
**Total endpoints:** ~40

### Auth — `/api/auth`
| Method | Path | Description |
|---|---|---|
| POST | `/login` | Staff login (phone + password) |
| POST | `/login-parent` | Parent login (phone + PIN) |
| PUT | `/set-pin` | Set PIN (authenticated) |
| POST | `/refresh` | Rotate refresh token |
| POST | `/logout` | Revoke refresh token |
| POST | `/forgot-password` | Trigger password reset email |
| POST | `/reset-password` | Complete password reset |
| POST | `/forgot-pin` | Trigger PIN reset email |
| POST | `/reset-pin` | Complete PIN reset |

### Attendance — `/api/attendance`
Mark absence (parent/admin), get attendance records (filtered by role), admin override.

### Homework — `/api/homework`
Create, list, and update homework assignments per class.

### Notices — `/api/notices`
Create draft, list published notices, publish a notice.

### Students — `/api/students`
Full CRUD: enroll, update, deactivate, get by class, get by parent, search parents by phone. Parent link management (link/unlink).

### Classes — `/api/classes`
List all classes for the authenticated user's school.

### Teachers — `/api/teachers`
List teachers, get profile, get assigned classes, assign/remove class assignments.

### Subjects — `/api/subjects`
List and create subjects for a school.

### Notifications — `/api/notifications`
Fetch feed, get unread count, mark one/all as read.

### Attachments — `/api/attachments`
Request pre-signed S3 upload URL, attach uploaded file to entity, list attachments, delete attachment.

---

## 7. Backend Code Structure

Each feature follows a consistent **Vertical Slice** pattern:

```
Features/
└── Homework/
    ├── CreateHomework/
    │   ├── CreateHomeworkCommand.cs        ← MediatR IRequest<T>
    │   ├── CreateHomeworkCommandHandler.cs ← IRequestHandler<T>
    │   ├── CreateHomeworkCommandValidator.cs ← FluentValidation
    │   └── CreateHomeworkEndpoint.cs       ← Minimal API handler
    ├── GetHomework/
    └── UpdateHomework/
```

**MediatR pipeline behaviors (applied globally):**
- `ValidationBehavior<,>` — runs FluentValidation before the handler, throws `ValidationException` on failure
- `LoggingBehavior<,>` — structured log on every command/query dispatch

**Common services:**
- `JwtTokenService` — generates access + refresh token pairs
- `PinService` — hashes and verifies parent PINs
- `PasswordHasher` — BCrypt wrapper for staff passwords
- `ResetTokenService` — generates secure reset tokens
- `CurrentUserService` — extracts `UserId`, `SchoolId`, `Role` from JWT claims via `IHttpContextAccessor`
- `DateTimeProvider` — server clock authority (injected, testable)
- `NotificationService` — creates in-app notification records
- `ResendEmailService` — sends transactional email via Resend HTTP API
- `S3StorageService` — generates pre-signed upload URLs and manages object lifecycle

**Middleware pipeline (in order):**
1. `UseRouting`
2. `UseSentryTracing` (if DSN configured)
3. `UseCorrelationId` — generates/propagates `X-Correlation-Id`
4. `UseRequestLogging` — logs method, path, status, duration
5. `UseGlobalException` — maps domain exceptions → RFC 7807 Problem Details
6. `UseAuthentication`
7. `UseRateLimiter` — 60 req/min per user (configurable), no limit on `/health`
8. `UseAuthorization`
9. `UseTenantIsolation` — validates `school_id` claim consistency
10. `UseCors`

**Security hardening in logs:**
A custom `DestructuringPolicy` redacts these fields from all Serilog output: `password`, `passwordhash`, `pinhash`, `secret`, `token`, `pin`, `phone`, `jwttoken`, `accesstoken`, `refreshtoken`, `jwt_secret`, `api_key`.

---

## 8. Frontend Structure

### Route Groups
```
app/
├── (auth)/              ← Unauthenticated layout
│   ├── login/
│   ├── forgot-password/
│   ├── reset-password/
│   ├── forgot-pin/
│   └── reset-pin/
├── (dashboard)/         ← Authenticated shell (PPR static)
│   ├── parent/
│   │   ├── attendance/
│   │   ├── homework/
│   │   └── notices/
│   ├── teacher/
│   │   ├── attendance/
│   │   ├── homework/
│   │   ├── students/[id]/
│   │   └── profile/
│   └── admin/
│       ├── students/         (list, new, [id], [id]/edit, [id]/link-parent)
│       ├── teachers/         (list, [id])
│       ├── notices/
│       └── subjects/
├── offline/             ← PWA offline page
└── page.tsx             ← Root redirect
```

### Key Frontend Modules
- `lib/api-client.ts` — typed fetch wrapper for the .NET API
- `lib/auth/jwt.ts` + `lib/auth/session.ts` — JWT decode + session management
- `lib/constants.ts` — app-wide constants
- `lib/validate-env.ts` — startup env validation (fails build if vars missing)
- `providers/auth-provider.tsx` — React context for auth state
- `components/auth/auth-guard.tsx` — route protection HOC
- `components/layout/` — `sidebar.tsx`, `header.tsx`, `bottom-nav.tsx`
- `components/shared/` — `student-card`, `student-list-page`, `notification-bell`, `notification-panel`, `attachment-uploader`, `attachment-list`, `class-selector`, `parent-link-list`, `empty-state`, `error-state`
- `components/ui/` — `button`, `card`, `input`, `label`, `badge`, `skeleton`, `spinner`
- `components/pwa/` — `install-prompt.tsx`, `sw-registrar.tsx`
- `hooks/` — `use-auth`, `use-notifications`, `use-student-list`, `use-media-query`

### PWA
Full PWA support with a custom service worker, Web App Manifest (`public/manifest.json`), and a complete icon set (72px to 512px, including maskable icons). Install prompt component for native app-like installation.

---

## 9. Infrastructure & DevOps

### Local Development
Docker Compose spins up three containers: PostgreSQL 16, the .NET API, and the Next.js frontend. The API applies EF Core migrations automatically on startup via `Database.MigrateAsync()`. Development seed SQL runs only in `Development` environment.

Convenience scripts are provided for both **bash** (Linux/macOS) and **PowerShell** (Windows) to manage the DB, run services, and execute tests.

### CI/CD — GitHub Actions

**`ci.yml`** runs on push to `main`/`develop` and on PRs:
- **Web job:** install → lint (ESLint) → type-check (tsc) → build (Next.js)
- **API job:** restore → build (.NET Release) → test (dotnet test, skipped gracefully if no tests)
- **Docker job:** (PRs only) build both Docker images to verify Dockerfile integrity
- **Migrations job:** spins up a test Postgres container, boots the API, and verifies startup succeeds after EF Core migrations apply

**`deploy.yml`** runs on push to `main`:
1. Calls `ci.yml` as a gate (must pass)
2. Deploys API to Railway (`railway up --service educonnect-api`)
3. Deploys Web to Railway (`railway up --service educonnect-web`)
4. Post-deploy health check: polls `/health` (API) and `/login` (Web) — fails the pipeline if either returns non-200

### Deployment Platform — Railway
Both services are containerised (Dockerfiles present in each app). `railway.toml` configures the health check path, start command, and restart policy (`on_failure`, max 3 retries).

---

## 10. Security Model

| Concern | Implementation |
|---|---|
| Access tokens | HS256 JWT, ≤ 15 min lifetime, stored in-memory only |
| Refresh tokens | Hashed in DB, HttpOnly + Secure + SameSite=Lax cookie, rotated on every use |
| Parent auth | Phone + PIN (4–6 digit, bcrypt-hashed) |
| Staff auth | Phone + password (BCrypt) |
| Tenant isolation | `school_id` on all tables, EF Core global query filters, `TenantIsolationMiddleware` |
| IDOR prevention | Every resource access validates `school_id` matches JWT claim |
| Rate limiting | 60 req/min per authenticated user (per IP for anonymous) |
| CORS | Explicit allowed origins from env var — no wildcard in production |
| Log redaction | 10 sensitive field patterns redacted from all Serilog output |
| Error responses | RFC 7807 Problem Details — zero stack traces in production HTTP responses |
| Reset flows | Secure time-limited tokens via `auth_reset_tokens` table + Resend email |

---

## 11. Shared Packages

**`packages/api-client`** — Placeholder for a generated TypeScript client from the OpenAPI spec. Generation is triggered via `pnpm openapi:generate`. The `src/generated/` directory currently contains only a `.gitkeep`.

**`packages/ui`** — Design token definitions in TypeScript:
- `colors.ts` — brand palette
- `spacing.ts` — spacing scale
- `typography.ts` — font sizes and weights

**`packages/config`** — Shared tooling configurations:
- `eslint/index.js` — shared ESLint rules
- `typescript/base.json` — strict TypeScript base config
- `tailwind/preset.js` — shared Tailwind preset

---

## 12. Engineering Laws (Hardcoded in ADR)

These rules are documented as non-negotiable:

1. **Every DB migration:** additive only — nullable column first, `NOT NULL` constraint in a separate deploy
2. **Every API change:** extend-only — never rename or remove existing fields
3. **Every log statement:** redact list enforced — zero credentials in output
4. **Every authenticated endpoint:** auth + authorization + tenant isolation checked before data access
5. **Every UI component:** WCAG 2.2 AA + keyboard navigable + ≥ 44px tap targets
6. **Every feature:** FCP < 1.5s and CLS = 0 confirmed before shipping

---

## 13. Current State Assessment

### What's Well-Built
- The architecture is thoughtful and well-documented for a solo project. ADRs are dated, justified, and include reversal cost ratings — an unusual level of rigor.
- Row-level multi-tenancy with EF Core global query filters is a solid, low-maintenance approach at this scale.
- The Vertical Slice pattern keeps feature code self-contained and easy to navigate.
- Security fundamentals are sound: refresh token rotation, HttpOnly cookies, log redaction, RFC 7807 errors.
- CI pipeline covers linting, type checking, build verification, Docker build, and SQL migration validation — comprehensive for a solo project.
- PWA support is complete (manifest, service worker, offline page, full icon set).

### Notable Gaps & Risks
- **No backend tests yet.** The CI pipeline handles the missing test directory gracefully (`echo "skipping"`), but `EduConnect.Api.Tests.csproj` exists as an empty scaffold. Tenant isolation and auth flows are the highest-risk areas without test coverage.
- **`packages/api-client` is empty.** The TypeScript client has not been generated from the OpenAPI spec yet. The frontend currently uses a hand-rolled `api-client.ts` rather than the generated typed client.
- **No mobile app.** `apps/mobile` (Expo/React Native) is referenced in the Product Genesis blueprint and ADR but is absent from the repository. The monorepo scaffold did not include it.
- **SQL migration numbering gap.** Migrations jump from `003` to `004`, then `006`–`009`, with no `002` or `005` in the schema folder (those numbers are used in the seed folder). This is not a bug, but the numbering could confuse future contributors.
- **No OpenAPI spec file.** `docs/API/openapi.yaml` is referenced in the blueprint but is not present in the repository. The TypeScript client generation script has nothing to generate from.
- **`is_editable` on homework is not automated.** The schema comment says it should be set to `FALSE` at end of day, but there is no background job or scheduled task to do this. It would need a cron job or database trigger.
- **Email service is hardcoded to Resend.** The `IEmailService` interface is injected, but there is only one implementation. If Resend is unavailable in a region or the account is suspended, there is no fallback.
- **Railway cold-start risk** is acknowledged in the Product Genesis document. The API is a .NET container which can have slow cold starts after inactivity. The health check endpoint mitigates this partially, but pre-warming is not automated.

---

## 14. Summary

EduConnect is a well-architected, production-grade school communication platform at an early but functional stage. The backend feature surface is nearly complete across all 11 domain areas. The frontend covers all three user roles across auth and dashboard flows. The infrastructure, CI/CD, and security model are production-ready. The main outstanding work is: populating the TypeScript API client from the OpenAPI spec, writing backend tests (especially tenant isolation), and building the mobile app.

The codebase reflects careful decision-making with documented trade-offs — a strong foundation for a solo product targeting Indian schools.
