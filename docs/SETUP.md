# EduConnect — Development Setup Guide

## Prerequisites

- Node.js 20+
- pnpm 9.15+
- .NET 8 SDK
- PostgreSQL 15+ (local or Railway)
- Git

## 1. Clone and Install

```bash
git clone <repo-url> educonnect
cd educonnect
pnpm install
```

## 2. Database Setup

### Option A: Railway (Recommended)

1. Create a new project at [railway.app](https://railway.app)
2. Add a PostgreSQL service
3. Copy the connection string from the Railway dashboard (Variables tab → DATABASE_URL)

### Option B: Local PostgreSQL

```bash
createdb educonnect
# Connection string: postgresql://your_user:your_password@localhost:5432/educonnect
```

### Option C: Docker Compose PostgreSQL

```bash
docker compose up db -d
# Connection string: postgresql://educonnect:educonnect_dev@localhost:5433/educonnect
```

### Switching between Local and Docker Postgres

The repo ships two ready-to-use env profiles so you can flip between a
native Postgres (port `5432`) and the Docker Postgres (host port `5433`)
without hand-editing `.env`:

| File | Profile | DATABASE_URL |
|------|---------|--------------|
| `.env.local`  | Native Postgres  | `postgresql://educonnect:educonnect_dev@localhost:5432/educonnect` |
| `.env.docker` | Docker Postgres  | `postgresql://educonnect:educonnect_dev@localhost:5433/educonnect` |

Switch with:

```bash
# Use natively installed Postgres (localhost:5432)
pnpm db:use:local

# Use Docker Postgres (localhost:5433); start it first with:
pnpm db:docker:up
pnpm db:use:docker

# Stop the Docker DB when done
pnpm db:docker:down
```

`pnpm db:use:*` copies the chosen profile file on top of `.env`
(backing up the previous one to `.env.bak`). After switching, run the
backend as usual:

```bash
pnpm local:backend:run         # macOS / Linux
pnpm local:backend:run:win     # Windows PowerShell
```

If you change profiles you should re-run migrations against the newly
active `DATABASE_URL` (see below).

### Run Migrations

```bash
# From project root, using psql:
psql "$DATABASE_URL" -f apps/api/src/EduConnect.Api/Infrastructure/Database/Migrations/001_foundation_tables.sql

# Seed development data (development only!):
psql "$DATABASE_URL" -f apps/api/src/EduConnect.Api/Infrastructure/Database/Migrations/002_seed_development_data.sql
```

### Verify Tables Created

```bash
psql "$DATABASE_URL" -c "\dt"
```

You should see 11 tables: schools, users, classes, students, teacher_class_assignments, parent_student_links, attendance_records, homework, notices, refresh_tokens, _migrations.

## 3. Environment Variables

```bash
cp .env.example .env
```

Edit `.env` and fill in:

| Variable | Required | Notes |
|----------|----------|-------|
| POSTGRES_HOST_PORT | No | Docker DB host port for local development. Default: `5433` |
| DATABASE_URL | Yes | PostgreSQL connection string from Railway |
| JWT_SECRET | Yes | Min 64 chars. Generate: `openssl rand -base64 64` |
| JWT_ISSUER | Yes | Default: `educonnect-api` |
| JWT_AUDIENCE | Yes | Default: `educonnect-client` |
| PIN_MIN_LENGTH | Yes | Default: `4` |
| PIN_MAX_LENGTH | Yes | Default: `6` |
| CORS_ALLOWED_ORIGINS | Yes | `http://localhost:3000` for development |
| NEXT_PUBLIC_API_URL | Yes | `http://localhost:5000` for development |
| NEXT_PUBLIC_APP_URL | Yes | `http://localhost:3000` for development |

## 4. Run the API (.NET 8)

```bash
cd apps/api/src/EduConnect.Api

# Set env vars (or use .env via direnv/dotenv)
export DATABASE_URL="postgresql://..."
export JWT_SECRET="your-64-char-secret-here"
export JWT_ISSUER="educonnect-api"
export JWT_AUDIENCE="educonnect-client"
export PIN_MIN_LENGTH="4"
export PIN_MAX_LENGTH="6"
export CORS_ALLOWED_ORIGINS="http://localhost:3000"

dotnet restore
dotnet run
```

API will start on `http://localhost:5000`. Verify: `curl http://localhost:5000/health`

## 5. Run the Web App (Next.js 15)

```bash
cd apps/web
pnpm dev
```

Web app starts on `http://localhost:3000`.

## 6. Development Seed Data Reference

After running the seed script, these accounts are available:

### Admin
| Name | Phone | Password | Role |
|------|-------|----------|------|
| Rajesh Kumar | 9000000001 | EduConnect@2026 | Admin |

### Teachers
| Name | Phone | Password | Teaches |
|------|-------|----------|---------|
| Priya Sharma | 9000000002 | EduConnect@2026 | Maths (5A), English (5B) |
| Anand Venkatesh | 9000000003 | EduConnect@2026 | Science (5A), Maths (6A) |

### Parents (PIN-based, use "1234" in dev mode)
| Name | Phone | PIN | Children |
|------|-------|-----|----------|
| Meena Devi | 9100000001 | 1234 | Arjun (5A), Kavitha (5A) |
| Suresh Babu | 9100000002 | 1234 | Kavitha (5A) |
| Lakshmi Narayan | 9100000003 | 1234 | Ravi (5A), Pooja (6A) |
| Karthik Rajan | 9100000004 | 1234 | Divya (5A) |
| Deepa Sundar | 9100000005 | 1234 | Sneha (5B), Arun (5B) |

### Classes
| Class | Section | Students |
|-------|---------|----------|
| 5 | A | Arjun, Kavitha, Ravi, Divya |
| 5 | B | Arun, Sneha, Vikram |
| 6 | A | Nithya, Sanjay, Pooja |

## 7. Useful Commands

```bash
# From project root:
pnpm dev              # Start all apps
pnpm dev:web          # Start web only
pnpm dev:api          # Start API only
pnpm build            # Build all
pnpm lint             # Lint all
pnpm type-check       # TypeScript check all

# API commands:
cd apps/api/src/EduConnect.Api
dotnet ef migrations add <Name>     # Create new EF migration
dotnet ef database update           # Apply pending migrations
```
