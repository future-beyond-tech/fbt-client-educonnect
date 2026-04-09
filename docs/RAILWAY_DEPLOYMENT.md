# Railway Deployment Guide — EduConnect

This document explains how to deploy both services of the EduConnect monorepo to [Railway](https://railway.app):

- **`apps/web`** — Next.js 15 frontend (Node 20 / pnpm)
- **`apps/api`** — ASP.NET Core 8 backend (.NET 8 SDK)
- **PostgreSQL** — managed Railway plugin

The repo is a pnpm + Turborepo monorepo, so each service is deployed as a **separate Railway service** pointing at the same GitHub repo but with a different **Root Directory** and **Dockerfile**.

---

## 1. Prerequisites

Before you start, make sure you have:

- A Railway account and a new (empty) project created.
- The GitHub repo connected to Railway (Settings → GitHub → Connect).
- The Railway CLI installed locally (optional but useful): `npm i -g @railway/cli`.
- Your production secrets ready (JWT key, database URL, Sentry DSN, etc.).

---

## 2. Project layout recap

```
fbt-client-educonnect/
├── apps/
│   ├── web/            # Next.js app   → Dockerfile here
│   └── api/            # .NET 8 API    → Dockerfile + railway.toml here
├── packages/           # Shared TS packages (only used by web)
├── pnpm-workspace.yaml
└── package.json
```

Both `apps/web/Dockerfile` and `apps/api/Dockerfile` are **self-contained** and use their own directory as the build context. This means Railway's **Root Directory** must be set to the specific app folder, not the repo root.

---

## 3. Provision the database first

1. In your Railway project, click **+ New → Database → Add PostgreSQL**.
2. Once provisioned, open the Postgres service → **Variables** tab and note:
   - `DATABASE_URL` (the full connection string)
   - `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `PGDATABASE`
3. You will reference these from the API service using Railway's `${{ Postgres.DATABASE_URL }}` variable syntax — no need to copy/paste secrets.

---

## 4. Deploy the API (`apps/api`)

### 4.1 Create the service

1. **+ New → GitHub Repo →** select `fbt-client-educonnect`.
2. Name the service `educonnect-api`.
3. Open **Settings**:
   - **Root Directory:** `apps/api`
   - **Builder:** Dockerfile
   - **Dockerfile Path:** `Dockerfile` (relative to root dir)
   - **Watch Paths:** `apps/api/**` (so web changes don't redeploy the API)

### 4.2 Environment variables

Set these in the **Variables** tab:

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` |
| `ConnectionStrings__DefaultConnection` | `${{ Postgres.DATABASE_URL }}` (or build the Npgsql string — see note below) |
| `Jwt__Key` | *your signing key (>= 32 chars)* |
| `Jwt__Issuer` | `https://educonnect-api.up.railway.app` |
| `Jwt__Audience` | `https://educonnect-web.up.railway.app` |
| `Cors__AllowedOrigins__0` | `https://educonnect-web.up.railway.app` |

> **Npgsql connection string note:** Railway's `DATABASE_URL` is in the Postgres URI format (`postgres://user:pass@host:port/db`). ASP.NET's Npgsql provider expects a key-value string. If your `Program.cs` doesn't already convert the URI, either (a) add a small converter on startup, or (b) set individual vars (`DB_HOST`, `DB_USER`, etc.) from `${{ Postgres.PGHOST }}` and build the string yourself.

### 4.3 Networking

1. Go to **Settings → Networking → Generate Domain**. This gives you a public URL like `educonnect-api.up.railway.app`.
2. The Dockerfile already exposes port **8080** and has a `HEALTHCHECK` on `/health`. Railway will auto-detect the port.

### 4.4 Deploy

Push to your main branch (or click **Deploy**). The build runs:

```
dotnet restore → dotnet publish -c Release → aspnet:8.0-alpine runtime
```

Tail logs in the **Deployments** tab until you see `Now listening on: http://[::]:8080`.

### 4.5 Run EF Core migrations

First deploy only — migrations need to run against the new database. Easiest options:

- **Option A (recommended):** call `db.Database.Migrate()` in `Program.cs` on startup so every deploy auto-applies pending migrations.
- **Option B:** run once manually from your machine:
  ```bash
  railway link            # pick educonnect-api
  railway run --service educonnect-api -- dotnet ef database update \
      --project apps/api/src/EduConnect.Api
  ```

---

## 5. Deploy the Web app (`apps/web`)

### 5.1 Create the service

1. **+ New → GitHub Repo →** select the same repo.
2. Name the service `educonnect-web`.
3. Open **Settings**:
   - **Root Directory:** `apps/web`
   - **Builder:** Dockerfile
   - **Dockerfile Path:** `Dockerfile`
   - **Watch Paths:** `apps/web/**`

> **Important — monorepo gotcha:** because the root directory is `apps/web`, nothing outside that folder is copied into the image. The `tsconfig.json` in `apps/web` must therefore be self-contained (no `extends` pointing at `../../packages/config/...`). This has already been fixed in the repo.

### 5.2 Build-time variables

Next.js inlines `NEXT_PUBLIC_*` vars at build time, so they must be set as **build args** and **runtime vars**. Add these in **Variables**:

| Variable | Value |
|---|---|
| `NEXT_PUBLIC_APP_URL` | `https://educonnect-web.up.railway.app` |
| `NEXT_PUBLIC_API_URL` | `https://educonnect-api.up.railway.app` |
| `NEXT_PUBLIC_SENTRY_DSN` | *your Sentry DSN (optional)* |
| `NODE_ENV` | `production` |
| `PORT` | `3000` |

The Dockerfile already declares matching `ARG`s, so Railway will pass them through automatically.

### 5.3 Networking

1. **Settings → Networking → Generate Domain** → get `educonnect-web.up.railway.app`.
2. Port **3000** is exposed by the Dockerfile.
3. Healthcheck path is `/login` (set in the Dockerfile `HEALTHCHECK` line).

### 5.4 Deploy

Push to main or click **Deploy**. The build runs:

```
pnpm install → pnpm build (next build, standalone output) → node server.js
```

Watch for `✓ Ready in …` in logs.

---

## 6. Wire the two services together

After both services are up:

1. Copy the **API public URL** and make sure `NEXT_PUBLIC_API_URL` on the web service matches it. If you change it, the web service will re-build.
2. Copy the **web public URL** and make sure the API has it in `Cors__AllowedOrigins__0` (and `Jwt__Audience` if you use it). Changing these only restarts the API — no rebuild needed.
3. Smoke-test:
   - `GET https://educonnect-api.up.railway.app/health` → `200 OK`
   - Open `https://educonnect-web.up.railway.app/login` → page renders, network tab shows requests going to the API domain.

---

## 7. Common issues

**`TS5083: Cannot read file '/packages/config/typescript/base.json'`**
The web `tsconfig.json` was extending a path outside the Docker build context. Fix: inline the base config into `apps/web/tsconfig.json` (already applied in this repo).

**`ECONNREFUSED` from web → api**
`NEXT_PUBLIC_API_URL` is baked in at build time. If you change it, you must redeploy the web service, not just restart it.

**CORS errors in the browser**
Make sure the *exact* web origin (scheme + host, no trailing slash) is listed in `Cors__AllowedOrigins__0` on the API.

**API can't reach the database**
Use Railway's variable reference `${{ Postgres.DATABASE_URL }}` rather than a hard-coded URL — it auto-updates if the DB is recreated. Also verify the Postgres plugin is in the *same* Railway project.

**Health check failing on web**
The Dockerfile checks `/login`. If you rename that route, update the `HEALTHCHECK` line in `apps/web/Dockerfile`.

**Build too slow / cache not working**
Railway caches Docker layers per-service. Keep `COPY package.json pnpm-lock.yaml*` (and `*.csproj` for .NET) above the `COPY . .` step so the dependency layer is reused across deploys — both Dockerfiles already do this.

---

## 8. Post-deploy checklist

- [ ] Postgres plugin attached and reachable from the API
- [ ] EF Core migrations applied
- [ ] API `/health` returns 200
- [ ] Web `/login` renders and can authenticate against the API
- [ ] Sentry receiving events (if configured)
- [ ] Both services have their own public domain
- [ ] `Watch Paths` set so each service only redeploys when relevant files change
- [ ] Secrets stored in Railway Variables, **never** in the repo

---

## 9. Useful Railway CLI commands

```bash
railway login
railway link                              # attach CWD to a project
railway status                            # show linked project/service
railway variables --service educonnect-api
railway logs --service educonnect-web
railway run --service educonnect-api -- dotnet ef database update \
    --project apps/api/src/EduConnect.Api
railway up                                # deploy current dir as a service
```

---

_Last updated: 2026-04-09_
