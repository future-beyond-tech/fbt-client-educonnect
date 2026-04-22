# EduConnect Development Setup Guide

## Current Repo Layout

The checked-in product currently includes:

- `apps/web` — Next.js 15 frontend
- `apps/api` — ASP.NET Core 8 API
- `packages/*` — shared TypeScript packages used by the web app

There is no `apps/mobile` app in this repo yet. Some older blueprint docs still mention it as a planned track.

## Prerequisites

- Node.js 20+
- pnpm 9.15+
- .NET 8 SDK
- PostgreSQL 15+ or Docker Desktop
- Git

## 1. Install Dependencies

```bash
git clone <repo-url> educonnect
cd educonnect
pnpm install
```

## 2. Choose a Database Profile

The repo ships with two ready-to-use local env profiles:

| Profile | File | DB Mode | Default URL |
|---|---|---|---|
| Native Postgres | `.env.local` | `local` | `postgresql://educonnect:educonnect_dev@localhost:5432/educonnect` |
| Docker Postgres | `.env.docker` | `docker` | `postgresql://educonnect:educonnect_dev@localhost:5433/educonnect` |

Switch between them with:

```bash
pnpm db:use:local
pnpm db:use:docker
```

That copies the selected profile onto `.env` and saves the previous file to `.env.bak`.

### Option A: Docker Postgres

Recommended for a clean local setup:

```bash
pnpm db:docker:up
pnpm db:use:docker
```

The backend wrapper script will also auto-start Docker Postgres when `EDUCONNECT_DB_MODE=docker`, so `pnpm local:backend:run` is usually enough once the profile is active.

### Option B: Native Postgres

If you already run Postgres locally:

```bash
pnpm db:use:local
createdb educonnect
```

Expected defaults:

- host: `localhost`
- port: `5432`
- user: `educonnect`
- password: `educonnect_dev`
- database: `educonnect`

Adjust `.env` if your local Postgres uses different credentials.

### Option C: Remote Postgres / Railway

Copy the example file and point it at a real remote database:

```bash
cp .env.example .env
```

Set these values in `.env`:

- `EDUCONNECT_DB_MODE=remote`
- `DATABASE_URL=<your real remote connection string>`

The helper scripts support `remote` mode for running the API locally against a hosted database, but lifecycle commands like `db:up` and `db:reset` intentionally remain local-only.

## 3. Environment Variables

The current startup contract is split across the frontend and backend.

### Required for local startup

| Variable | Used By | Notes |
|---|---|---|
| `DATABASE_URL` | API | Supports PostgreSQL URI or Npgsql-style connection strings |
| `JWT_SECRET` | API | Must be at least 64 characters |
| `JWT_ISSUER` | API | Default local value: `educonnect-api` |
| `JWT_AUDIENCE` | API | Default local value: `educonnect-client` |
| `PIN_MIN_LENGTH` | API startup validation | Current app code still enforces 4-6 digit PINs |
| `PIN_MAX_LENGTH` | API startup validation | Current app code still enforces 4-6 digit PINs |
| `CORS_ALLOWED_ORIGINS` | API | Comma-separated origins |
| `RATE_LIMIT_API_PER_USER_PER_MINUTE` | API | Default local value: `60` |
| `NEXT_PUBLIC_APP_URL` | API + web | Used for reset links and frontend routing |
| `NEXT_PUBLIC_API_URL` | web | Used by the frontend API client |
| `RESEND_API_KEY` | API | Required at startup; local scripts/env profiles use a dev placeholder |
| `RESEND_FROM_EMAIL` | API | Required at startup; replace the placeholder to test real email delivery |

### Optional but supported

| Variable | Used By | Notes |
|---|---|---|
| `SENTRY_DSN` | API | Enables server-side Sentry reporting |
| `NEXT_PUBLIC_SENTRY_DSN` | web | Enables browser/edge/server Sentry for Next.js |
| `SENTRY_ORG` / `SENTRY_PROJECT` | web build | Needed for Sentry source map upload when `NEXT_PUBLIC_SENTRY_DSN` is set |
| `S3_SERVICE_URL` | API | Use for S3-compatible storage such as R2 or MinIO |
| `S3_ACCESS_KEY` / `S3_SECRET_KEY` | API | Used with `S3_SERVICE_URL` |
| `AWS_REGION` | API | Used when talking to AWS S3 directly; defaults to `ap-south-1` |
| `NEXT_PUBLIC_ATTACHMENT_UPLOAD_ORIGINS` | web build | Comma-separated storage origins allowed by CSP for presigned browser uploads; useful when the web build cannot read `S3_SERVICE_URL` directly |

Notes:

- `.env.example` is shell-safe and can be sourced by the Bash helper scripts.
- `.env.local` and `.env.docker` now include placeholder `RESEND_*` values so the API can boot locally without real mail credentials.
- Forgot/reset email delivery will not work end to end until you replace those placeholders with real Resend settings.

## 4. Run the App Locally

Use two terminals.

### Backend

Recommended wrapper:

```bash
pnpm local:backend:run
```

When the API runs in `Development`, it also serves Swagger JSON at `http://localhost:5000/openapi/v1.json`. The shared `packages/api-client` artifacts are generated from that document.

What it does:

- loads `.env`
- fills in local defaults for missing dev-safe vars
- auto-starts Docker Postgres when DB mode is `docker`
- waits for `http://localhost:5000/health`

Useful variants:

```bash
scripts/run-backend-local.sh --dry-run
scripts/run-backend-local.sh --db local
scripts/run-backend-local.sh --db remote --no-db-autostart
```

### Frontend

Recommended wrapper:

```bash
pnpm local:frontend:run
```

This runs Next.js on `http://localhost:3000`.

Useful variant:

```bash
scripts/run-frontend-local.sh --dry-run
```

### Important command differences

- `pnpm local:backend:run` is the safest way to run the API locally.
- `pnpm dev:api` is a lower-level `dotnet run` shortcut and expects your env to already be exported.
- `pnpm dev` and `pnpm dev:web` currently start only the web app because `apps/api` is not part of the pnpm workspace.

## 5. Database Migrations and Seed Data

This repo now standardizes on **EF Core migrations** for schema changes.

Source of truth:

```text
apps/api/src/EduConnect.Api/Migrations/
```

Runtime behavior:

- On API startup, `dbContext.Database.MigrateAsync()` applies any pending EF Core migrations automatically.
- In `Development`, the API then executes the SQL files in `apps/api/src/EduConnect.Api/Infrastructure/Database/Migrations/seed/`.
- Those seed SQL files are the only SQL scripts the running app executes directly.

Common workflows:

- Start the API normally with `pnpm local:backend:run` and let startup apply migrations for you.
- Apply pending EF migrations manually with `pnpm db:update`.

Adding a new schema migration:

1. Create an EF Core migration:

   ```bash
   dotnet ef migrations add <MigrationName> \
     --project apps/api/src/EduConnect.Api/EduConnect.Api.csproj \
     --startup-project apps/api/src/EduConnect.Api/EduConnect.Api.csproj
   ```

2. Review the generated files under `apps/api/src/EduConnect.Api/Migrations/`.
3. Apply them with `pnpm db:update` or by restarting the API.

Notes:

- Keep `seed/*.sql` idempotent, because they are replayed on Development startup.

## 6. Development Seed Data

After the API boots successfully in `Development`, the seed migrations provide:

### Staff login

| Role | Phone | Password |
|---|---|---|
| Admin | `09000000001` | `EduConnect@2026` |
| Teacher | `09000000002` | `EduConnect@2026` |
| Teacher | `09000000003` | `EduConnect@2026` |

### Parent login

| Role | Phone | PIN |
|---|---|---|
| Parent | `09100000001` | `1234` |
| Parent | `09100000002` | `1234` |
| Parent | `09100000003` | `1234` |
| Parent | `09100000004` | `1234` |
| Parent | `09100000005` | `1234` |

### Classes and students

- `5-A`: Arjun Meena, Kavitha Suresh, Ravi Lakshmi, Divya Karthik
- `5-B`: Arun Rajan, Sneha Deepa, Vikram Anand
- `6-A`: Nithya Venkat, Sanjay Kumar, Pooja Narayan

Important:

- The seed data does **not** populate user email addresses by default.
- If you want to test `/forgot-password` or `/forgot-pin`, first add an email to the relevant seeded user in the database and configure real `RESEND_*` credentials.

## 7. Useful Commands

```bash
# Recommended local run commands
pnpm local:backend:run
pnpm local:frontend:run

# Validation
pnpm local:backend:test
pnpm local:frontend:test
pnpm test

# Build outputs
pnpm build
pnpm build:web
pnpm build:api
pnpm openapi:generate

# Web-only shortcuts
pnpm dev
pnpm dev:web
pnpm lint
pnpm type-check

# Direct API shortcut (expects env already present)
pnpm dev:api

# Database helpers
pnpm db:up
pnpm db:down
pnpm db:reset
pnpm db:status
pnpm db:psql
pnpm db:url
```

## 8. Troubleshooting

### Backend fails immediately with missing env vars

Check `.env` first. The most common misses are:

- `NEXT_PUBLIC_APP_URL`
- `RESEND_API_KEY`
- `RESEND_FROM_EMAIL`
- `DATABASE_URL`

### `pnpm dev` does not start the API

That is current behavior. Use:

- `pnpm local:backend:run`
- `pnpm local:frontend:run`

### Forgot/reset flows return success but no email arrives

Make sure all three are true:

- the user has an email address in `users.email`
- `RESEND_API_KEY` is real
- `RESEND_FROM_EMAIL` is a verified sender in Resend

### Attachment upload features fail

Configure storage first:

- AWS S3: provide normal AWS credentials plus `AWS_REGION`
- S3-compatible storage: provide `S3_SERVICE_URL`, `S3_ACCESS_KEY`, and `S3_SECRET_KEY`
