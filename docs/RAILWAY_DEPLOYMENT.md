# Railway Deployment Guide

EduConnect deploys as two Railway services from the same repo:

- `apps/api` — ASP.NET Core 8 API
- `apps/web` — Next.js 15 frontend

PostgreSQL should be provisioned as a separate Railway database service and shared with the API.

## 1. What Matters in This Repo

Current implementation details that affect deployment:

- the API reads **flat env vars** such as `DATABASE_URL` and `JWT_SECRET`
- the API automatically applies EF Core migrations on startup
- the frontend bakes `NEXT_PUBLIC_*` values in at build time
- each app ships its own Dockerfile and `railway.toml`

Older docs or generic Railway guides that mention `ConnectionStrings__DefaultConnection`, `Jwt__Key`, or `Cors__AllowedOrigins__0` do **not** match this repo's current code.

## 2. Provision PostgreSQL First

Create a PostgreSQL service in Railway, then keep its `DATABASE_URL` available for the API service.

The API can consume Railway's standard PostgreSQL URI directly. `DatabaseConnectionStringResolver` converts it into the Npgsql format at startup, so you do not need a manual connection-string converter.

## 3. Deploy the API Service

### Service settings

- Root Directory: `apps/api`
- Builder: `Dockerfile`
- Dockerfile Path: `Dockerfile`

The checked-in `apps/api/railway.toml` expects:

- health check: `/health`
- port: `8080`
- start command: `dotnet EduConnect.Api.dll`

### Required API environment variables

| Variable | Suggested Railway value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` |
| `DATABASE_URL` | `${{ Postgres.DATABASE_URL }}` |
| `JWT_SECRET` | a random 64+ char secret |
| `JWT_ISSUER` | your API public URL |
| `JWT_AUDIENCE` | your web public URL or chosen audience string |
| `PIN_MIN_LENGTH` | `4` |
| `PIN_MAX_LENGTH` | `6` |
| `RATE_LIMIT_API_PER_USER_PER_MINUTE` | `60` |
| `CORS_ALLOWED_ORIGINS` | your web public origin |
| `NEXT_PUBLIC_APP_URL` | your web public URL |
| `RESEND_API_KEY` | your real Resend API key |
| `RESEND_FROM_EMAIL` | a verified Resend sender |

### Optional API variables

| Variable | Use |
|---|---|
| `SENTRY_DSN` | server-side Sentry reporting |
| `S3_SERVICE_URL` | S3-compatible storage endpoint |
| `S3_ACCESS_KEY` / `S3_SECRET_KEY` | credentials for S3-compatible storage |
| `AWS_REGION` | AWS S3 region when not using `S3_SERVICE_URL` |

### Migration behavior

No manual migration step is required for Railway deploys. On every boot, the API:

- runs `dbContext.Database.MigrateAsync()` to apply pending EF Core migrations
- runs `Infrastructure/Database/Migrations/seed/*.sql` only in `Development`

That means production deploys should rely on the built-in startup runner. `dotnet ef database update` remains a valid manual maintenance command for local or one-off admin workflows, but it is not required in Railway deploy automation.

## 4. Deploy the Web Service

### Service settings

- Root Directory: `apps/web`
- Builder: `Dockerfile`
- Dockerfile Path: `Dockerfile`

The checked-in `apps/web/railway.toml` expects:

- health check: `/login`
- port: `3000`
- start command: `node server.js`

### Required web environment variables

| Variable | Suggested Railway value |
|---|---|
| `NEXT_PUBLIC_APP_URL` | your web public URL |
| `NEXT_PUBLIC_API_URL` | your API public URL |

### Optional web variables

| Variable | Use |
|---|---|
| `NEXT_PUBLIC_SENTRY_DSN` | browser/edge/server Sentry in Next.js |
| `SENTRY_ORG` | Sentry source map upload |
| `SENTRY_PROJECT` | Sentry source map upload |
| `NODE_ENV` | usually `production` |

Notes:

- `NEXT_PUBLIC_*` values are compiled into the frontend during `next build`.
- If you change `NEXT_PUBLIC_API_URL`, redeploy the web service so the new value is baked into the build.
- The checked-in Swagger/OpenAPI route (`/openapi/v1.json`) is enabled only in `Development`; its absence in Railway `Production` is expected.

## 5. Smoke Test After Deploy

Verify these URLs after both services are up:

- `GET <api-url>/health` returns `200`
- `GET <web-url>/login` renders the login page
- login requests from the browser go to the deployed API URL

## 6. Common Failure Modes

### API fails on startup with missing env vars

The API validates its env contract before building the host. Double-check:

- `DATABASE_URL`
- `JWT_SECRET`
- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `CORS_ALLOWED_ORIGINS`
- `NEXT_PUBLIC_APP_URL`
- `RESEND_API_KEY`
- `RESEND_FROM_EMAIL`

### API cannot reach Railway Postgres

Use Railway's service reference:

```text
DATABASE_URL=${{ Postgres.DATABASE_URL }}
```

Do not switch to `ConnectionStrings__DefaultConnection`; the current code does not read it.

### Browser shows CORS errors

Set `CORS_ALLOWED_ORIGINS` to the exact web origin, including scheme and hostname, with no trailing slash.

### Forgot/reset email flows never send mail

Startup only checks that `RESEND_*` is present. Real delivery still requires:

- a valid Resend API key
- a verified sender/domain
- users with email addresses in the database

### Attachment uploads fail

The attachment endpoints depend on object storage configuration. Make sure you supply either:

- AWS credentials + `AWS_REGION`, or
- `S3_SERVICE_URL` + `S3_ACCESS_KEY` + `S3_SECRET_KEY`
