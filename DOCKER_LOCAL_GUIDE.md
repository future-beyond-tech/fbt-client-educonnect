# EduConnect — Docker Local Run & Test Guide
**Last updated:** April 13, 2026

This guide covers two modes:

- **Mode A — Full Docker** (all three services in containers): best for a clean, isolated test run
- **Mode B — Hybrid** (Docker DB only, local .NET and Next.js processes): best for active development with fast restarts

---

## Prerequisites

Before starting, verify all tools are installed:

```bash
# Check Docker
docker --version           # Docker 24+ recommended
docker compose version     # Docker Compose v2+ (bundled with Docker Desktop)

# Check Node + pnpm
node --version             # must be >= 20.0.0
pnpm --version             # must be >= 9.15.0

# Check .NET SDK (for Mode B only)
dotnet --version           # must be 8.0.x
```

If pnpm is not installed:
```bash
npm install -g pnpm@9.15.0
```

---

## Step 1 — Clone & Install Dependencies

```bash
# Clone (skip if already done)
git clone <your-repo-url> educonnect
cd educonnect

# Install all Node dependencies
pnpm install
```

You should see pnpm resolve the workspace (apps/web + packages/*).

---

## Step 2 — Set Up Environment Variables

Copy the example env file and set the minimum required values:

```bash
cp .env.example .env
```

Open `.env` and verify or set these values (the file already has working defaults for Docker mode):

```bash
# These are already correct for Docker mode — verify they match:
EDUCONNECT_DB_MODE=docker
POSTGRES_HOST_PORT=5433
DATABASE_URL=postgresql://educonnect:educonnect_dev@localhost:5433/educonnect

NODE_ENV=development
NEXT_PUBLIC_APP_URL=http://localhost:3000
NEXT_PUBLIC_API_URL=http://localhost:5000

ASPNETCORE_ENVIRONMENT=Development

# JWT — MUST be 64+ characters. Replace this placeholder:
JWT_SECRET=replace-with-a-random-64-plus-character-secret-key-here-make-it-long
JWT_ISSUER=educonnect-api
JWT_AUDIENCE=educonnect-client

PIN_MIN_LENGTH=4
PIN_MAX_LENGTH=6

CORS_ALLOWED_ORIGINS=http://localhost:3000
RATE_LIMIT_API_PER_USER_PER_MINUTE=60

# Email — placeholder is fine for local testing (forgot-password won't send real email)
RESEND_API_KEY=dev-resend-api-key
RESEND_FROM_EMAIL="EduConnect <no-reply@example.com>"

# Leave Sentry blank for local
SENTRY_DSN=
NEXT_PUBLIC_SENTRY_DSN=

# Leave S3 blank for local (attachment uploads will fail but everything else works)
S3_SERVICE_URL=
S3_ACCESS_KEY=
S3_SECRET_KEY=
AWS_REGION=ap-south-1
```

> **JWT_SECRET tip:** Generate a secure secret quickly:
> ```bash
> openssl rand -base64 64
> # or on any machine:
> node -e "console.log(require('crypto').randomBytes(64).toString('base64'))"
> ```

---

## Mode A — Full Docker (All Services)

This mode builds and runs the Database, .NET API, and Next.js Web all inside Docker containers.

### Step A1 — Build and Start All Containers

```bash
docker compose up --build
```

This will:
1. Pull `postgres:16-alpine`
2. Build the `apps/api` image using its multi-stage Dockerfile
3. Build the `apps/web` image using its multi-stage Dockerfile
4. Start all three containers in dependency order (db → api → web)

> **First run takes 3–8 minutes** as it downloads base images and compiles .NET + Next.js.  
> Subsequent runs are much faster due to Docker layer caching.

### Step A2 — Verify Everything Is Up

Open a second terminal and run:

```bash
# All three containers should show "Up (healthy)" or "Up"
docker compose ps

# Check API health
curl http://localhost:5000/health

# Check Web is serving
curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/login
# Expected: 200
```

Expected API health response:
```json
{ "status": "ok", "version": "...", "db": "connected" }
```

### Step A3 — View Logs

```bash
# All services together
docker compose logs -f

# API only
docker compose logs -f api

# Web only
docker compose logs -f web

# DB only
docker compose logs -f db
```

### Step A4 — Stop All Containers

```bash
# Stop but keep volumes (DB data is preserved)
docker compose down

# Stop AND destroy all data (fresh slate)
docker compose down -v
```

---

## Mode B — Hybrid (Docker DB + Local Processes)

This is the recommended mode for active development. Faster hot-reload and easier debugging.

### Step B1 — Start Only the Database Container

```bash
pnpm db:docker:up
# or directly:
docker compose up db -d
```

Verify the DB is ready:
```bash
docker compose ps db
# Should show: healthy
```

### Step B2 — Activate the Docker DB Profile

```bash
pnpm db:use:docker
# This copies .env.docker onto .env and saves the old .env to .env.bak
```

### Step B3 — Run the Backend (Terminal 1)

```bash
pnpm local:backend:run
```

This script:
- Loads `.env`
- Fills in any missing dev-safe defaults
- Auto-starts Docker Postgres if not already running
- Runs `dotnet run` pointing at the API project
- Waits until `http://localhost:5000/health` responds

You should see Serilog output streaming in the terminal. Look for:
```
[INFO] SqlMigrationRunner: Applying schema migration 001_foundation_tables.sql ... done
[INFO] SqlMigrationRunner: Applying seed migration 002_seed_development_data.sql ... done
[INFO] Now listening on: http://localhost:5000
```

### Step B4 — Run the Frontend (Terminal 2)

```bash
pnpm local:frontend:run
```

This runs Next.js dev server at `http://localhost:3000`.

---

## Step 3 — Seed Data & Login Credentials

Once both services are running, the database is automatically seeded with development data. **No manual setup required.**

### Staff Login (Phone + Password)

| Role | Phone | Password | School |
|---|---|---|---|
| Admin | `09000000001` | `EduConnect@2026` | Dev School |
| Teacher | `09000000002` | `EduConnect@2026` | Dev School |
| Teacher | `09000000003` | `EduConnect@2026` | Dev School |

### Parent Login (Phone + PIN)

| Phone | PIN | Linked Students |
|---|---|---|
| `09100000001` | `1234` | Arjun Meena (5-A) |
| `09100000002` | `1234` | Kavitha Suresh (5-A) |
| `09100000003` | `1234` | Ravi Lakshmi (5-A) |
| `09100000004` | `1234` | Arun Rajan (5-B) |
| `09100000005` | `1234` | Sneha Deepa (5-B) |

### Classes & Students

| Class | Students |
|---|---|
| 5-A | Arjun Meena, Kavitha Suresh, Ravi Lakshmi, Divya Karthik |
| 5-B | Arun Rajan, Sneha Deepa, Vikram Anand |
| 6-A | Nithya Venkat, Sanjay Kumar, Pooja Narayan |

---

## Step 4 — Test All API Endpoints via curl

All API tests below assume the API is running at `http://localhost:5000`.

### 4.1 — Auth: Staff Login

```bash
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"phone":"09000000001","password":"EduConnect@2026"}' | python3 -m json.tool
```

Save the `accessToken` from the response:
```bash
TOKEN="paste-access-token-here"
```

### 4.2 — Auth: Parent Login (PIN)

```bash
curl -s -X POST http://localhost:5000/api/auth/login-parent \
  -H "Content-Type: application/json" \
  -d '{"phone":"09100000001","pin":"1234"}' | python3 -m json.tool
```

Save parent's `accessToken`:
```bash
PARENT_TOKEN="paste-parent-access-token-here"
```

### 4.3 — Health Check

```bash
curl http://localhost:5000/health
```

### 4.4 — Get Classes (Admin/Teacher)

```bash
curl -s http://localhost:5000/api/classes \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

Note the `id` of class **5-A** — save it:
```bash
CLASS_ID="paste-class-id-here"
```

### 4.5 — Get Students by Class

```bash
curl -s "http://localhost:5000/api/students?classId=$CLASS_ID" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

Note a student `id` from the list:
```bash
STUDENT_ID="paste-student-id-here"
```

### 4.6 — Get Student by ID

```bash
curl -s "http://localhost:5000/api/students/$STUDENT_ID" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### 4.7 — Enroll a New Student

```bash
curl -s -X POST http://localhost:5000/api/students \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"classId\":\"$CLASS_ID\",\"name\":\"Test Student\",\"rollNumber\":\"T99\"}" | python3 -m json.tool
```

### 4.8 — Mark Attendance (as Parent)

```bash
curl -s -X POST http://localhost:5000/api/attendance \
  -H "Authorization: Bearer $PARENT_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"studentId\":\"$STUDENT_ID\",\"date\":\"$(date +%Y-%m-%d)\",\"status\":\"Absent\",\"reason\":\"Fever\"}" | python3 -m json.tool
```

### 4.9 — Get Attendance

```bash
curl -s "http://localhost:5000/api/attendance?studentId=$STUDENT_ID" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### 4.10 — Get Students for Parent (as Parent)

```bash
curl -s http://localhost:5000/api/students/my-children \
  -H "Authorization: Bearer $PARENT_TOKEN" | python3 -m json.tool
```

### 4.11 — Create Homework (as Teacher)

First get the Teacher token:
```bash
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"phone":"09000000002","password":"EduConnect@2026"}' | python3 -m json.tool

TEACHER_TOKEN="paste-teacher-access-token-here"
```

```bash
curl -s -X POST http://localhost:5000/api/homework \
  -H "Authorization: Bearer $TEACHER_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"classId\":\"$CLASS_ID\",\"subject\":\"Mathematics\",\"title\":\"Chapter 5 Exercises\",\"description\":\"Complete all exercises on page 45\",\"dueDate\":\"$(date -d '+3 days' +%Y-%m-%d 2>/dev/null || date -v+3d +%Y-%m-%d)\"}" | python3 -m json.tool
```

### 4.12 — Get Homework

```bash
curl -s "http://localhost:5000/api/homework?classId=$CLASS_ID" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### 4.13 — Create a Notice (as Admin)

```bash
curl -s -X POST http://localhost:5000/api/notices \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Parent-Teacher Meeting","body":"PTM scheduled for Saturday 10 AM. All parents are requested to attend.","targetAudience":"All"}' | python3 -m json.tool
```

Save the notice `id`:
```bash
NOTICE_ID="paste-notice-id-here"
```

### 4.14 — Publish a Notice

```bash
curl -s -X PUT "http://localhost:5000/api/notices/$NOTICE_ID/publish" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### 4.15 — Get Notices (as Parent)

```bash
curl -s http://localhost:5000/api/notices \
  -H "Authorization: Bearer $PARENT_TOKEN" | python3 -m json.tool
```

### 4.16 — Get Teachers (as Admin)

```bash
curl -s http://localhost:5000/api/teachers \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### 4.17 — Get Subjects

```bash
curl -s http://localhost:5000/api/subjects \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### 4.18 — Get Notifications

```bash
curl -s http://localhost:5000/api/notifications \
  -H "Authorization: Bearer $PARENT_TOKEN" | python3 -m json.tool

# Unread count
curl -s http://localhost:5000/api/notifications/unread-count \
  -H "Authorization: Bearer $PARENT_TOKEN"
```

### 4.19 — Test Token Refresh

```bash
# The refresh token is stored in an HttpOnly cookie.
# Use -c to save and -b to send cookies:
curl -s -c cookies.txt -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"phone":"09000000001","password":"EduConnect@2026"}'

# Now refresh using the cookie
curl -s -b cookies.txt -c cookies.txt -X POST http://localhost:5000/api/auth/refresh | python3 -m json.tool
```

### 4.20 — Test Unauthenticated Access (expect 401)

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/api/students
# Expected: 401
```

### 4.21 — Logout

```bash
curl -s -b cookies.txt -X POST http://localhost:5000/api/auth/logout \
  -H "Authorization: Bearer $TOKEN"
```

---

## Step 5 — Test the Web UI

Open your browser at `http://localhost:3000`.

### UI Test Flows

**Flow 1 — Admin**
1. Go to `http://localhost:3000/login`
2. Enter phone `09000000001`, password `EduConnect@2026` → click Login
3. You should land on the Admin dashboard
4. Navigate to **Students** → verify the class list and student list load
5. Click **New Student** → fill in the form → Submit
6. Navigate to **Teachers** → verify teacher list with class assignments
7. Navigate to **Notices** → create a notice → publish it
8. Navigate to **Subjects** → verify subject list loads

**Flow 2 — Teacher**
1. Logout (or open a private/incognito window)
2. Login with phone `09000000002`, password `EduConnect@2026`
3. Navigate to **My Classes** → select class 5-A
4. Navigate to **Attendance** → mark a student absent
5. Navigate to **Homework** → create a new homework assignment
6. Navigate to **Profile** → verify your profile and class assignments

**Flow 3 — Parent**
1. Open a new private window, go to `http://localhost:3000/login`
2. Enter phone `09100000001`, PIN `1234` → click Login
3. You should see the Parent dashboard
4. Navigate to **Attendance** → check your child's attendance history
5. Navigate to **Homework** → see assignments for your child's class
6. Navigate to **Notices** → verify the published notice appears
7. Click the notification bell (🔔) → verify notifications appear

**Flow 4 — PWA Install**
1. In Chrome, open `http://localhost:3000`
2. Look for the install prompt in the browser address bar or the PWA install banner
3. Install the app — it should open as a standalone window

---

## Step 6 — Run Automated Tests

### Frontend Tests (lint + type-check + build)

```bash
pnpm local:frontend:test
```

Or individually:
```bash
pnpm lint
pnpm type-check
```

### Backend Tests

```bash
pnpm local:backend:test
# or:
dotnet test apps/api/tests/EduConnect.Api.Tests/EduConnect.Api.Tests.csproj -c Release
```

> **Note:** The test project is currently a scaffold — no tests are written yet. This command will complete with 0 tests run. See the full analysis report for recommended test areas.

### SQL Migration Lint (same as CI)

```bash
# Requires a running Postgres instance
pnpm db:status

# Manually run migrations against a test DB to verify idempotency:
for file in apps/api/src/EduConnect.Api/Infrastructure/Database/Migrations/schema/*.sql; do
  echo "Testing: $file"
  # Run against your local DB — idempotent, safe to replay
done
```

---

## Step 7 — Database Management Commands

```bash
# Check DB status
pnpm db:status

# Open psql shell (inspect tables directly)
pnpm db:psql

# Once in psql — useful queries:
\dt                                    # list all tables
SELECT * FROM schools;
SELECT id, name, role, phone FROM users;
SELECT * FROM students LIMIT 10;
SELECT * FROM attendance_records LIMIT 10;
SELECT * FROM homework LIMIT 5;
SELECT * FROM notices LIMIT 5;

# Reset DB (drops all data + re-runs migrations + seed on next API boot)
pnpm db:reset

# Stop Docker DB
pnpm db:docker:down

# Print the current DATABASE_URL
pnpm db:url
```

---

## Step 8 — Tenant Isolation Verification

This is the most critical security test — verify that one school cannot see another school's data.

```bash
# The seed data only creates one school, so this is a manual test:
# 1. Connect to psql
pnpm db:psql

# 2. Insert a second school and a user
INSERT INTO schools (name, code) VALUES ('Test School B', 'TSB001');

# 3. Get the new school ID
SELECT id FROM schools WHERE code = 'TSB001';

# 4. Verify that the Admin from School A (school_id in their JWT) 
#    cannot see students, notices, or homework from School B — 
#    the TenantIsolationMiddleware and EF Core global query filters
#    will automatically scope all queries to the JWT's school_id.
```

---

## Troubleshooting

### API container exits immediately
Check the logs: `docker compose logs api`  
Most common cause: missing or invalid `JWT_SECRET` (must be 64+ characters).

### "relation does not exist" errors in API logs
The migration runner failed. Check: `docker compose logs api | grep Migration`  
Try resetting: `docker compose down -v && docker compose up --build`

### Web shows "Failed to fetch" or network errors
Verify `NEXT_PUBLIC_API_URL` in `.env` matches where the API is running:
- Full Docker: `http://localhost:5000`
- Hybrid mode: `http://localhost:5000`
- If running inside Docker network: use the service name `http://api:8080`

### `pnpm dev` doesn't start the API
This is expected. The `pnpm dev` command only starts the Next.js web app. Always use:
```bash
pnpm local:backend:run   # for API
pnpm local:frontend:run  # for web
```

### Port conflicts
Default ports used:
- `5433` — PostgreSQL (Docker)
- `5000` — .NET API
- `3000` — Next.js Web

If any port is in use:
```bash
# Find the process using port 5000
lsof -i :5000
# Or on Windows:
netstat -ano | findstr :5000
```

### Forgot-password / reset-pin email not arriving
Expected — the seed data has no email addresses, and `RESEND_API_KEY` is a placeholder. To test email flows:
1. Add an email to a user in psql: `UPDATE users SET email = 'your@email.com' WHERE phone = '09000000001';`
2. Replace `RESEND_API_KEY` and `RESEND_FROM_EMAIL` in `.env` with real Resend credentials
3. Restart the API

### Attachment uploads failing
Expected without S3 configuration. To enable:
1. Set `S3_SERVICE_URL`, `S3_ACCESS_KEY`, `S3_SECRET_KEY` in `.env` (for R2/MinIO)
2. Or set `AWS_REGION` and configure AWS credentials for native S3
3. Restart the API
