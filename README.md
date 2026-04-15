# EduConnect

School communication platform — attendance, homework, notices.

## Prerequisites

- **Node.js**: >= 20
- **pnpm**: `pnpm@9` (repo pins `pnpm@9.15.0`)
- **.NET SDK**: required to run the API locally (`apps/api`)
- **Docker** (optional): for running Postgres (and optionally the full stack) via `docker compose`

## Quick start (recommended: local scripts)

1) Install dependencies:

```bash
pnpm install
```

2) Create your env file:

```bash
cp .env.example .env
```

3) Start the backend API (auto-starts Docker Postgres by default):

```bash
pnpm local:backend:run
```

4) Start the web app:

```bash
pnpm local:frontend:run
```

- **Web**: `http://localhost:3000`
- **API**: `http://localhost:5000` (healthcheck: `/health`)

## Alternative: run via Turbo (monorepo)

Run everything that supports `dev` under Turbo:

```bash
pnpm dev
```

Or run individual targets:

```bash
pnpm dev:web
pnpm dev:api
```

## Database (Postgres)

By default the repo is set up to use a Docker Postgres on host port **5433**.

- Start DB:

```bash
pnpm db:up
```

- Stop DB:

```bash
pnpm db:down
```

- Reset DB:

```bash
pnpm db:reset
```

- Open `psql`:

```bash
pnpm db:psql
```

The API applies schema + seed migrations automatically on startup.

## Full stack with Docker Compose (optional)

This runs **db + api + web** with Docker:

```bash
docker compose up --build
```

## Common commands

- **Lint (web)**:

```bash
pnpm lint
```

- **Type-check (web)**:

```bash
pnpm type-check
```

- **Tests**:

```bash
pnpm test
```

- **Build**:

```bash
pnpm build
```

## E2E tests (web)

```bash
pnpm test:e2e:web
```

## Repo layout

- `apps/web`: Next.js app
- `apps/api`: .NET API
- `packages/*`: shared packages (UI, API client, config, etc.)
