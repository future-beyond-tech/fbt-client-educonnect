# EduConnect — Product Genesis Blueprint

> Historical note: this document captures the sprint-zero blueprint from 2026-04-03. It is no longer the operational source of truth. For current setup and deployment behavior, use [`docs/SETUP.md`](./SETUP.md) and [`docs/RAILWAY_DEPLOYMENT.md`](./RAILWAY_DEPLOYMENT.md).
>
> Current implementation differences from this blueprint:
> - the checked-in repo currently ships `apps/web` and `apps/api` only; there is no `apps/mobile` app yet
> - the API uses additive SQL files under `Infrastructure/Database/Migrations/` with auto-apply on startup, not EF migration scaffolding as the primary workflow
> - features added after genesis now exist in code, including subjects, teacher management, student management, notifications, attachments, and password/PIN reset flows

**Date:** 2026-04-03
**Phase:** Idea → Sprint Zero
**Initial Target Stack:** Next.js 15 (PPR) + Expo | .NET 8 Minimal API (VSA + CQRS + MediatR) | PostgreSQL (Railway)

## Current Repository Snapshot (2026-04-13)

```text
fbt-client-educonnect/
├── apps/
│   ├── web/            # checked-in Next.js 15 frontend
│   └── api/            # checked-in ASP.NET Core 8 API
├── packages/
│   ├── api-client/     # placeholder package; client generation not wired yet
│   ├── ui/
│   └── config/
├── docs/
│   ├── SETUP.md
│   ├── RAILWAY_DEPLOYMENT.md
│   └── ADR/
└── scripts/            # local run/test/db helpers for macOS/Linux + Windows
```

---

## PHASE 2 — REPOSITORY SCAFFOLD

### 2.1 Repository Structure

```
educonnect/
├── apps/
│   ├── web/                              # Next.js 15 — PPR frontend
│   │   ├── app/
│   │   │   ├── (auth)/
│   │   │   │   └── login/
│   │   │   │       └── page.tsx
│   │   │   ├── (dashboard)/
│   │   │   │   ├── layout.tsx            # Authenticated shell (static via PPR)
│   │   │   │   ├── parent/
│   │   │   │   │   ├── attendance/
│   │   │   │   │   │   └── page.tsx
│   │   │   │   │   ├── homework/
│   │   │   │   │   │   └── page.tsx
│   │   │   │   │   └── notices/
│   │   │   │   │       └── page.tsx
│   │   │   │   ├── teacher/
│   │   │   │   │   ├── homework/
│   │   │   │   │   │   └── page.tsx
│   │   │   │   │   └── attendance/
│   │   │   │   │       └── page.tsx
│   │   │   │   └── admin/
│   │   │   │       ├── notices/
│   │   │   │       │   └── page.tsx
│   │   │   │       ├── students/
│   │   │   │       │   └── page.tsx
│   │   │   │       └── teachers/
│   │   │   │           └── page.tsx
│   │   │   ├── layout.tsx                # Root layout — font, metadata, providers
│   │   │   ├── error.tsx                 # Error boundary
│   │   │   ├── global-error.tsx          # Root error boundary
│   │   │   ├── not-found.tsx
│   │   │   └── page.tsx                  # Landing / redirect to dashboard
│   │   ├── components/
│   │   │   ├── ui/                       # Base design system (Button, Input, Card, etc.)
│   │   │   ├── layout/                   # Shell, Sidebar, Header
│   │   │   └── shared/                   # Skeleton, ErrorState, EmptyState
│   │   ├── lib/
│   │   │   ├── api-client.ts             # Typed fetch wrapper for .NET API
│   │   │   ├── auth.ts                   # Token management, refresh logic
│   │   │   ├── validate-env.ts           # Startup env validation
│   │   │   └── constants.ts
│   │   ├── hooks/
│   │   │   ├── use-auth.ts
│   │   │   └── use-api.ts
│   │   ├── providers/
│   │   │   └── auth-provider.tsx
│   │   ├── public/
│   │   ├── tailwind.config.ts
│   │   ├── next.config.ts
│   │   ├── tsconfig.json
│   │   └── package.json
│   │
│   ├── mobile/                           # Expo (React Native)
│   │   ├── app/                          # Expo Router (file-based)
│   │   │   ├── (auth)/
│   │   │   ├── (tabs)/
│   │   │   └── _layout.tsx
│   │   ├── components/
│   │   ├── lib/
│   │   ├── hooks/
│   │   ├── app.json
│   │   ├── tsconfig.json
│   │   └── package.json
│   │
│   └── api/                              # .NET 8 Minimal API
│       ├── src/
│       │   └── EduConnect.Api/
│       │       ├── Features/             # VSA — each feature owns everything
│       │       │   ├── Auth/
│       │       │   │   ├── Login/
│       │       │   │   │   ├── LoginCommand.cs
│       │       │   │   │   ├── LoginCommandHandler.cs
│       │       │   │   │   ├── LoginCommandValidator.cs
│       │       │   │   │   └── LoginEndpoint.cs
│       │       │   │   ├── RefreshToken/
│       │       │   │   │   ├── RefreshTokenCommand.cs
│       │       │   │   │   ├── RefreshTokenCommandHandler.cs
│       │       │   │   │   └── RefreshTokenEndpoint.cs
│       │       │   │   └── Logout/
│       │       │   │       ├── LogoutCommand.cs
│       │       │   │       ├── LogoutCommandHandler.cs
│       │       │   │       └── LogoutEndpoint.cs
│       │       │   ├── Attendance/
│       │       │   │   ├── MarkAbsence/
│       │       │   │   │   ├── MarkAbsenceCommand.cs
│       │       │   │   │   ├── MarkAbsenceCommandHandler.cs
│       │       │   │   │   ├── MarkAbsenceCommandValidator.cs
│       │       │   │   │   └── MarkAbsenceEndpoint.cs
│       │       │   │   ├── GetAttendance/
│       │       │   │   │   ├── GetAttendanceQuery.cs
│       │       │   │   │   ├── GetAttendanceQueryHandler.cs
│       │       │   │   │   └── GetAttendanceEndpoint.cs
│       │       │   │   └── AdminOverride/
│       │       │   │       ├── AdminOverrideCommand.cs
│       │       │   │       ├── AdminOverrideCommandHandler.cs
│       │       │   │       ├── AdminOverrideCommandValidator.cs
│       │       │   │       └── AdminOverrideEndpoint.cs
│       │       │   ├── Homework/
│       │       │   │   ├── CreateHomework/
│       │       │   │   │   ├── CreateHomeworkCommand.cs
│       │       │   │   │   ├── CreateHomeworkCommandHandler.cs
│       │       │   │   │   ├── CreateHomeworkCommandValidator.cs
│       │       │   │   │   └── CreateHomeworkEndpoint.cs
│       │       │   │   ├── GetHomework/
│       │       │   │   │   ├── GetHomeworkQuery.cs
│       │       │   │   │   ├── GetHomeworkQueryHandler.cs
│       │       │   │   │   └── GetHomeworkEndpoint.cs
│       │       │   │   └── UpdateHomework/
│       │       │   │       ├── UpdateHomeworkCommand.cs
│       │       │   │       ├── UpdateHomeworkCommandHandler.cs
│       │       │   │       ├── UpdateHomeworkCommandValidator.cs
│       │       │   │       └── UpdateHomeworkEndpoint.cs
│       │       │   └── Notices/
│       │       │       ├── CreateNotice/
│       │       │       │   ├── CreateNoticeCommand.cs
│       │       │       │   ├── CreateNoticeCommandHandler.cs
│       │       │       │   ├── CreateNoticeCommandValidator.cs
│       │       │       │   └── CreateNoticeEndpoint.cs
│       │       │       ├── GetNotices/
│       │       │       │   ├── GetNoticesQuery.cs
│       │       │       │   ├── GetNoticesQueryHandler.cs
│       │       │       │   └── GetNoticesEndpoint.cs
│       │       │       └── PublishNotice/
│       │       │           ├── PublishNoticeCommand.cs
│       │       │           ├── PublishNoticeCommandHandler.cs
│       │       │           └── PublishNoticeEndpoint.cs
│       │       ├── Common/
│       │       │   ├── Auth/
│       │       │   │   ├── JwtTokenService.cs
│       │       │   │   ├── PinService.cs
│       │       │   │   ├── PasswordHasher.cs
│       │       │   │   └── CurrentUserService.cs
│       │       │   ├── Middleware/
│       │       │   │   ├── CorrelationIdMiddleware.cs
│       │       │   │   ├── RequestLoggingMiddleware.cs
│       │       │   │   ├── GlobalExceptionMiddleware.cs
│       │       │   │   └── TenantIsolationMiddleware.cs
│       │       │   ├── Behaviors/
│       │       │   │   ├── ValidationBehavior.cs         # MediatR pipeline
│       │       │   │   └── LoggingBehavior.cs
│       │       │   ├── Extensions/
│       │       │   │   ├── ServiceCollectionExtensions.cs
│       │       │   │   └── EndpointRouteBuilderExtensions.cs
│       │       │   └── Models/
│       │       │       ├── ProblemDetails.cs              # RFC 7807
│       │       │       ├── PagedResult.cs
│       │       │       └── Result.cs                      # Result<T> pattern
│       │       ├── Infrastructure/
│       │       │   ├── Database/
│       │       │   │   ├── AppDbContext.cs
│       │       │   │   ├── Entities/
│       │       │   │   │   ├── SchoolEntity.cs
│       │       │   │   │   ├── UserEntity.cs
│       │       │   │   │   ├── StudentEntity.cs
│       │       │   │   │   ├── ClassEntity.cs
│       │       │   │   │   ├── AttendanceEntity.cs
│       │       │   │   │   ├── HomeworkEntity.cs
│       │       │   │   │   ├── NoticeEntity.cs
│       │       │   │   │   └── RefreshTokenEntity.cs
│       │       │   │   ├── Configurations/               # EF Core Fluent API
│       │       │   │   │   ├── SchoolConfiguration.cs
│       │       │   │   │   ├── UserConfiguration.cs
│       │       │   │   │   ├── StudentConfiguration.cs
│       │       │   │   │   ├── ClassConfiguration.cs
│       │       │   │   │   ├── AttendanceConfiguration.cs
│       │       │   │   │   ├── HomeworkConfiguration.cs
│       │       │   │   │   ├── NoticeConfiguration.cs
│       │       │   │   │   └── RefreshTokenConfiguration.cs
│       │       │   │   └── Migrations/
│       │       │   └── Services/
│       │       │       └── DateTimeProvider.cs            # Server clock authority
│       │       ├── Program.cs
│       │       ├── appsettings.json
│       │       ├── appsettings.Development.json
│       │       └── EduConnect.Api.csproj
│       ├── tests/
│       │   └── EduConnect.Api.Tests/
│       │       ├── Features/
│       │       │   ├── Auth/
│       │       │   ├── Attendance/
│       │       │   ├── Homework/
│       │       │   └── Notices/
│       │       ├── Common/
│       │       │   └── TenantIsolationTests.cs
│       │       └── EduConnect.Api.Tests.csproj
│       └── EduConnect.sln
│
├── packages/
│   ├── api-client/                       # Generated TypeScript client
│   │   ├── src/
│   │   │   └── generated/
│   │   ├── package.json
│   │   └── tsconfig.json
│   ├── ui/                               # Shared design tokens
│   │   ├── tokens/
│   │   │   ├── colors.ts
│   │   │   ├── spacing.ts
│   │   │   └── typography.ts
│   │   ├── package.json
│   │   └── tsconfig.json
│   └── config/
│       ├── eslint/
│       │   └── index.js
│       ├── typescript/
│       │   └── base.json
│       └── tailwind/
│           └── preset.js
│
├── docs/
│   ├── ADR/
│   │   └── 001-architecture-decisions.md
│   ├── API/
│   │   └── openapi.yaml
│   ├── PRODUCT-GENESIS.md
│   └── SETUP.md
│
├── .env.example
├── .gitignore
├── package.json                          # pnpm workspaces root
├── pnpm-workspace.yaml
└── turbo.json                            # Turborepo for monorepo task orchestration
```

### 2.2 Initial DB Schema (EF Core Migrations — Migration 001)

All tables include `school_id` for row-level tenant isolation.
All timestamps use `TIMESTAMPTZ` (server clock is authority).
Critical data uses soft deletes (`is_deleted`, `deleted_at`).

```sql
-- Migration 001: Foundation Tables
-- EduConnect — Additive-only discipline starts NOW.

-- ══════════════════════════════════════════════
-- TENANT: Schools
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS schools (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(200) NOT NULL,
    code            VARCHAR(20) NOT NULL UNIQUE,       -- short school code for onboarding
    address         TEXT,
    contact_phone   VARCHAR(20),
    contact_email   VARCHAR(200),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ══════════════════════════════════════════════
-- USERS: Parents, Teachers, Admins
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    phone           VARCHAR(20) NOT NULL,
    name            VARCHAR(200) NOT NULL,
    role            VARCHAR(20) NOT NULL CHECK (role IN ('Parent', 'Teacher', 'Admin')),
    password_hash   VARCHAR(500),                      -- NULL for parents (PIN-based)
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (school_id, phone)
);

CREATE INDEX IF NOT EXISTS idx_users_school_id ON users(school_id);
CREATE INDEX IF NOT EXISTS idx_users_phone ON users(phone);

-- ══════════════════════════════════════════════
-- CLASSES
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS classes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    name            VARCHAR(50) NOT NULL,               -- e.g. "5A", "10B"
    section         VARCHAR(10),
    academic_year   VARCHAR(10) NOT NULL,                -- e.g. "2026-27"
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (school_id, name, section, academic_year)
);

CREATE INDEX IF NOT EXISTS idx_classes_school_id ON classes(school_id);

-- ══════════════════════════════════════════════
-- TEACHER ↔ CLASS ASSIGNMENTS
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS teacher_class_assignments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    teacher_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    class_id        UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    subject         VARCHAR(100) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (teacher_id, class_id, subject)
);

CREATE INDEX IF NOT EXISTS idx_tca_school_id ON teacher_class_assignments(school_id);
CREATE INDEX IF NOT EXISTS idx_tca_teacher_id ON teacher_class_assignments(teacher_id);

-- ══════════════════════════════════════════════
-- STUDENTS
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS students (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    class_id        UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    roll_number     VARCHAR(20) NOT NULL,
    name            VARCHAR(200) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (school_id, class_id, roll_number)
);

CREATE INDEX IF NOT EXISTS idx_students_school_id ON students(school_id);
CREATE INDEX IF NOT EXISTS idx_students_class_id ON students(class_id);

-- ══════════════════════════════════════════════
-- PARENT ↔ STUDENT BINDING (immutable)
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS parent_student_links (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    parent_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    student_id      UUID NOT NULL REFERENCES students(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (parent_id, student_id)
);

CREATE INDEX IF NOT EXISTS idx_psl_parent_id ON parent_student_links(parent_id);

-- ══════════════════════════════════════════════
-- ATTENDANCE (append-only, soft delete)
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS attendance_records (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    student_id      UUID NOT NULL REFERENCES students(id) ON DELETE CASCADE,
    date            DATE NOT NULL,
    status          VARCHAR(20) NOT NULL CHECK (status IN ('Absent', 'Late')),
    reason          TEXT,                               -- optional for parent, mandatory for admin
    entered_by_id   UUID NOT NULL REFERENCES users(id),
    entered_by_role VARCHAR(20) NOT NULL,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (student_id, date)                          -- one record per student per day
);

CREATE INDEX IF NOT EXISTS idx_attendance_school_id ON attendance_records(school_id);
CREATE INDEX IF NOT EXISTS idx_attendance_student_date ON attendance_records(student_id, date);

-- ══════════════════════════════════════════════
-- HOMEWORK (soft delete)
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS homework (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    class_id        UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    subject         VARCHAR(100) NOT NULL,
    title           VARCHAR(300) NOT NULL,
    description     TEXT NOT NULL,
    assigned_by_id  UUID NOT NULL REFERENCES users(id),
    due_date        DATE,
    published_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_editable     BOOLEAN NOT NULL DEFAULT TRUE,      -- set to FALSE at end of day
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_homework_school_id ON homework(school_id);
CREATE INDEX IF NOT EXISTS idx_homework_class_id ON homework(class_id);

-- ══════════════════════════════════════════════
-- NOTICES (immutable after publish, soft delete)
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS notices (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    title           VARCHAR(300) NOT NULL,
    body            TEXT NOT NULL,
    target_audience VARCHAR(50) NOT NULL CHECK (target_audience IN ('All', 'Class', 'Section')),
    target_class_id UUID REFERENCES classes(id),        -- NULL if target = 'All'
    published_by_id UUID NOT NULL REFERENCES users(id),
    is_published    BOOLEAN NOT NULL DEFAULT FALSE,
    published_at    TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_notices_school_id ON notices(school_id);
CREATE INDEX IF NOT EXISTS idx_notices_published ON notices(is_published, published_at);

-- ══════════════════════════════════════════════
-- REFRESH TOKENS
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      VARCHAR(500) NOT NULL,              -- NEVER store plaintext
    expires_at      TIMESTAMPTZ NOT NULL,
    is_revoked      BOOLEAN NOT NULL DEFAULT FALSE,
    revoked_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    replaced_by_id  UUID REFERENCES refresh_tokens(id)  -- rotation chain
);

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user ON refresh_tokens(user_id);
```

### 2.3 Environment Variable Manifest

```bash
# .env.example — EduConnect
# ALL variables required. App MUST fail to start if any are missing.
# Never commit .env — only .env.example

# ── APP ──────────────────────────────────────
NODE_ENV=development
NEXT_PUBLIC_APP_URL=http://localhost:3000
NEXT_PUBLIC_API_URL=http://localhost:5000

# ── .NET API ─────────────────────────────────
ASPNETCORE_ENVIRONMENT=Development
API_PORT=5000

# ── DATABASE ─────────────────────────────────
POSTGRES_HOST_PORT=5433
DATABASE_URL=postgresql://user:pass@localhost:5433/educonnect

# ── JWT ──────────────────────────────────────
JWT_SECRET=                              # min 64 chars, cryptographically random
JWT_ISSUER=educonnect-api
JWT_AUDIENCE=educonnect-client
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7

# ── PIN (Parent Auth) ────────────────────────
PIN_MIN_LENGTH=4
PIN_MAX_LENGTH=6

# ── RATE LIMITING ────────────────────────────
RATE_LIMIT_API_PER_USER_PER_MINUTE=60

# ── OBSERVABILITY ────────────────────────────
LOG_LEVEL=info                           # debug | info | warn | error
SENTRY_DSN=
SENTRY_ENVIRONMENT=development

# ── CORS ─────────────────────────────────────
CORS_ALLOWED_ORIGINS=http://localhost:3000
```

### 2.4 Startup Validation

**Next.js (web app):**

```typescript
// apps/web/lib/validate-env.ts

const REQUIRED_SERVER = [
  'NEXT_PUBLIC_API_URL',
] as const;

const REQUIRED_BUILD = [
  'NEXT_PUBLIC_APP_URL',
] as const;

export function validateEnv(): void {
  const missing = [...REQUIRED_SERVER, ...REQUIRED_BUILD]
    .filter((key) => !process.env[key]);

  if (missing.length > 0) {
    throw new Error(
      `[EduConnect Web] Missing required environment variables:\n${missing.join('\n')}`
    );
  }
}
```

**.NET 8 API:**

```csharp
// apps/api/src/EduConnect.Api/Common/Extensions/ServiceCollectionExtensions.cs

public static class EnvironmentValidation
{
    private static readonly string[] RequiredVars =
    [
        "DATABASE_URL",
        "JWT_SECRET",
        "JWT_ISSUER",
        "JWT_AUDIENCE"
    ];

    public static void ValidateEnvironment()
    {
        List<string> missing = RequiredVars
            .Where(key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"[EduConnect API] Missing required environment variables: {string.Join(", ", missing)}"
            );
        }
    }
}

// Called at the top of Program.cs BEFORE building the app:
// EnvironmentValidation.ValidateEnvironment();
```

---

## PHASE 3 — FOUNDATION LAYERS

Each layer must be complete before the next. Implementation order matters.

### Layer 1: Observability (build first — you cannot debug what you cannot see)

```
[x] Structured logging — Serilog with JSON formatter, correlation ID on every log line
[x] Log levels: debug | info | warn | error — configurable via LOG_LEVEL env var
[x] REDACT list: password, token, cookie, secret, authorization, x-api-key, pin, code_hash
[x] Error tracking — Sentry configured, never lose an uncaught exception
[x] Health check: GET /health → { status: "ok", version, uptime, db: "connected" }
[x] Request logging middleware: method, path, status, duration, correlationId
[x] LOG FORMAT: { level, timestamp, correlationId, service: "educonnect-api", message, ...context }
```

### Layer 2: Authentication (zero-trust from day one)

```
[x] Custom JWT generation with role claim, school_id claim, user_id claim
[x] PIN flow: validate phone + PIN → issue JWT pair (parents)
[x] Credentials flow: validate phone + password → issue JWT pair (teachers/admins)
[x] Access token: ≤ 15 minutes, in response body (frontend stores in memory only)
[x] Refresh token: HttpOnly, Secure, SameSite=Lax cookie — NEVER localStorage
[x] Refresh token rotation: new refresh token on every refresh, old one revoked
[x] Session invalidation: revoke all refresh tokens for user (immediate effect)
[x] Protected route middleware: rejects unauthenticated requests with 401
[x] Role-based authorization: [Authorize(Roles = "Admin")] equivalent per endpoint
[x] NEVER log: accessToken, refreshToken, pin, password_hash, code_hash
```

### Layer 3: Multi-Tenancy Guard

```
[x] school_id extracted from JWT claim on every authenticated request
[x] CurrentUserService exposes: UserId, SchoolId, Role — available via DI
[x] TenantIsolationMiddleware: validates school_id on every request
[x] Base query helper: all queries automatically filter by school_id
[x] IDOR check: every resource access validates school_id matches JWT claim
[x] Integration test: User from School A cannot see School B data — must pass before launch
```

### Layer 4: Error Handling

```
[x] GlobalExceptionMiddleware: catches all unhandled exceptions
[x] Maps domain errors → HTTP status codes (ValidationException → 400, NotFoundException → 404, ForbiddenException → 403)
[x] RFC 7807 Problem Details for ALL API errors:
    { type, title, status, detail, instance, traceId }
[x] Next.js error boundaries: error.tsx (route-level) + global-error.tsx (root)
[x] Every error log includes: full exception, correlationId, userId (if authed), schoolId
[x] Production: NO stack traces in HTTP responses — internal logging only
```

### Layer 5: Design System Foundation

```
[x] Tailwind CSS + shadcn/ui configured
[x] CSS variables defined: --primary, --secondary, --destructive, --muted, --accent
[x] Font loaded via next/font (Inter for body, no @import)
[x] Base components: Button, Input, Label, Card, Badge, Spinner, Skeleton
[x] Each component: WCAG 2.2 AA, keyboard navigable, aria-labelled, ≥ 44px tap targets
[x] Skeleton loading component: matches final layout (CLS = 0)
[x] ErrorState component: clear message + retry action
[x] EmptyState component: context-appropriate per feature
[x] Framer Motion configured with prefers-reduced-motion → duration: 0 fallback
[x] Spring physics only: standard { stiffness: 400, damping: 30, mass: 1 }
```

### Layer 6: API Skeleton

```
[x] OpenAPI spec: docs/API/openapi.yaml — all MVP endpoints defined
[x] Endpoints: Auth (4), Attendance (3), Homework (3), Notices (3), Health (1) = 14 endpoints
[x] TypeScript client generation script: pnpm openapi:generate
[x] CORS: explicit allowed origins from CORS_ALLOWED_ORIGINS env var (never wildcard in prod)
[x] Rate limiting: auth endpoints (strict), read endpoints (moderate), write endpoints (standard)
[x] Input validation: FluentValidation on every command/query via MediatR ValidationBehavior
[x] Extend-only contract discipline from first endpoint
```

---

## PHASE 4 — SPRINT ZERO EXECUTION PLAN

```
SPRINT ZERO — EduConnect
Goal: Foundation complete. One feature deployed end-to-end. Zero tech debt.
Timeline: 7 working days

─── MILESTONE 1: REPO + ENVIRONMENT (Day 1)
  [ ] M1.1 — Create monorepo: pnpm init, pnpm-workspace.yaml, turbo.json
  [ ] M1.2 — Scaffold apps/web (Next.js 15 with PPR enabled)
  [ ] M1.3 — Scaffold apps/api (.NET 8 Minimal API with EduConnect.Api.csproj)
  [ ] M1.4 — Scaffold apps/mobile (Expo with Expo Router)
  [ ] M1.5 — Configure packages/config (ESLint, TypeScript strict, Tailwind preset)
  [ ] M1.6 — Create .env.example with all required variables
  [ ] M1.7 — Configure .gitignore (node_modules, .env, bin, obj, .next)
  [ ] M1.8 — Startup validation: both web and API fail fast on missing env vars

─── MILESTONE 2: DATABASE (Day 1–2)
  [ ] M2.1 — Provision Railway PostgreSQL instance
  [ ] M2.2 — Add EF Core 8 + Npgsql to API project
  [ ] M2.3 — Create AppDbContext with all entity configurations
  [ ] M2.4 — Create Migration 001: all foundation tables (schema above)
  [ ] M2.5 — Run migration against Railway DB — confirm all tables created
  [ ] M2.6 — Seed: 1 school, 1 admin, 2 teachers, 3 classes, 10 students, 5 parents
  [ ] M2.7 — Confirm DATABASE_URL works in all environments

─── MILESTONE 3: AUTH (Day 2–3)
  [ ] M3.1 — Implement JwtTokenService (generate access + refresh tokens)
  [ ] M3.2 — Implement PinService (hash, verify parent PINs)
  [ ] M3.3 — Implement PasswordHasher (BCrypt for teacher/admin credentials)
  [ ] M3.4 — Build Auth feature slices: Login, RefreshToken, Logout (phone + PIN/password)
  [ ] M3.5 — Refresh token: HttpOnly Secure cookie, rotation on every refresh
  [ ] M3.6 — CurrentUserService: extracts UserId, SchoolId, Role from JWT claims
  [ ] M3.7 — Test: unauthenticated request → 401 (not 500)
  [ ] M3.8 — Test: expired access token + valid refresh → new token pair
  [ ] M3.9 — Test: revoked refresh token → 401, forces re-login

─── MILESTONE 4: OBSERVABILITY (Day 3)
  [ ] M4.1 — Add Serilog with JSON sink + console sink
  [ ] M4.2 — CorrelationIdMiddleware: generates/propagates X-Correlation-Id
  [ ] M4.3 — RequestLoggingMiddleware: logs method, path, status, duration
  [ ] M4.4 — Configure REDACT list in Serilog destructuring policy
  [ ] M4.5 — GET /health endpoint returning status, version, uptime, db connectivity
  [ ] M4.6 — Sentry configured (API + Web) — test with deliberate throw
  [ ] M4.7 — Confirm: zero token/secret values appear in any log output

─── MILESTONE 5: CI/CD (Day 3–4)
  [ ] M5.1 — Railway service: educonnect-api (.NET 8)
  [ ] M5.2 — Railway service: educonnect-web (Next.js 15)
  [ ] M5.3 — Environment variables set in Railway dashboard
  [ ] M5.4 — Deploy pipeline: push to main → auto deploy both services
  [ ] M5.5 — Health check endpoint returns 200 on deployed API
  [ ] M5.6 — Web app loads on deployed URL (even if just login page)

─── MILESTONE 6: DESIGN SYSTEM (Day 4–5)
  [ ] M6.1 — Tailwind config + CSS tokens + shadcn/ui initialized
  [ ] M6.2 — next/font configured (Inter) — no layout shift
  [ ] M6.3 — Base components: Button, Input, Label, Card, Badge — WCAG 2.2 AA
  [ ] M6.4 — Skeleton, ErrorState, EmptyState components
  [ ] M6.5 — Auth pages: Login (phone + PIN/password)
  [ ] M6.6 — Dashboard shell: sidebar + header + role-based navigation
  [ ] M6.7 — Framer Motion configured with reduced-motion check
  [ ] M6.8 — Lighthouse score ≥ 90 on login page

─── MILESTONE 7: FIRST FEATURE — ATTENDANCE (Day 5–7)
  [ ] M7.1 — OpenAPI spec: MarkAbsence, GetAttendance, AdminOverride endpoints
  [ ] M7.2 — TypeScript client generated from spec
  [ ] M7.3 — Backend: MarkAbsenceCommandHandler (validates one-per-day, no future dates, audit trail)
  [ ] M7.4 — Backend: GetAttendanceQueryHandler (filtered by school_id + parent's student_ids)
  [ ] M7.5 — Backend: TenantIsolationMiddleware verified — parent sees only their children
  [ ] M7.6 — Frontend: Parent attendance page (mark absence + view history)
  [ ] M7.7 — Frontend: Skeleton loading → data → empty state flows
  [ ] M7.8 — End-to-end test: parent logs in via PIN → marks absence → sees it in history
  [ ] M7.9 — Performance: FCP < 1.5s, CLS = 0 on attendance page

SPRINT ZERO DONE WHEN:
  → All 7 milestones complete
  → EduConnect deployed to Railway (API + Web)
  → Parent can: login via PIN → mark child's absence → view attendance history
  → Zero known P0 gaps
  → ADR document committed to /docs/ADR/
  → No TypeScript `any` in codebase
  → No secrets in logs
```

---

## FIRST RISK TO WATCH

**Railway cold start latency.** If Railway's cold start on the .NET API takes longer than expected, the first request after a deployment may timeout, creating a poor DX experience. **Mitigation:** Monitor Railway logs during Day 1 deployment. Pre-warm the API with a health check endpoint. Consider implementing circuit breaker and retry logic on the frontend to gracefully handle transient failures.

---

## ONGOING ENGINEERING LAWS (these never change)

- Every DB migration: additive only — nullable first, NOT NULL in separate deploy
- Every API change: extend-only — never rename or remove existing fields
- Every log statement: redact list enforced — zero credentials in output
- Every authenticated endpoint: auth + authorization + tenant isolation before data access
- Every UI component: WCAG 2.2 AA + UX laws applied before shipping
- Every feature: FCP < 1.5s, CLS = 0 confirmed — no regressions
