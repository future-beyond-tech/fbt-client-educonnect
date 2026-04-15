═══════════════════════════════════════════════════
EDUCONNECT — FULL FEATURE AUDIT REPORT
Generated: 2026-04-15
Audited by: FBT Engineering / Claude Cowork
═══════════════════════════════════════════════════

REPOSITORY OVERVIEW
───────────────────
Repo type:         Monorepo (pnpm workspaces + Turborepo)
Backend project:   EduConnect.Api  →  apps/api/src/EduConnect.Api/
Frontend project:  @educonnect/web →  apps/web/
Shared packages:   packages/api-client, packages/ui, packages/config
Total .cs files:   261
Total .tsx files:  69
Total migrations:  11 schema + 3 seed = 14 SQL migration files (raw SQL runner, not EF migrations)
DB tables:         schools, users, classes, teacher_class_assignments, students,
                   parent_student_links, attendance_records, homework, notices,
                   refresh_tokens, auth_reset_tokens, subjects, notifications,
                   attachments, leave_applications
                   (15 tables total)

───────────────────────────────────────────────────
FEATURE STATUS MATRIX
───────────────────────────────────────────────────

────────────────
CORE FEATURES
────────────────

FEATURE: Authentication & Login
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Custom JWT auth (NOT Zentra). Staff login via email+password, parent login via phone+PIN.
             Refresh token stored as HttpOnly cookie. Automatic silent refresh 2 minutes before expiry.
             ForgotPassword, ResetPassword, ForgotPin, ResetPin flows all backed by Resend transactional email.
  Key files: apps/api/src/.../Features/Auth/Login/LoginCommandHandler.cs
             apps/api/src/.../Common/Auth/JwtTokenService.cs
             apps/web/app/(auth)/login/page.tsx
             apps/web/providers/auth-provider.tsx

FEATURE: Role-Based Access Control (RBAC)
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Three roles: Admin, Teacher, Parent. Role claim embedded in JWT.
             TenantIsolationMiddleware extracts userId/schoolId/role from JWT into CurrentUserService.
             Frontend AuthGuard enforces role-based routing — redirects to role's default landing route.
             Each role sees a different nav (Admin: notices/students/classes/teachers;
             Teacher: homework/attendance/students/profile; Parent: attendance/homework/notices).
  Key files: apps/api/src/.../Common/Middleware/TenantIsolationMiddleware.cs
             apps/web/components/auth/auth-guard.tsx
             apps/web/lib/constants.ts (Roles, navigationByRole, defaultRouteByRole)

FEATURE: Attendance Tracking
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Mark absence (Teacher), admin override, get attendance history, apply leave (Parent),
             get leave applications. Attendance model: status is "Absent" or "Late" only
             (Present is implied by absence of a record). Leave applications have Pending/Approved/Rejected
             workflow. Teacher attendance page and parent attendance view both wired to live API.
  Key files: apps/api/src/.../Features/Attendance/MarkAbsence/MarkAbsenceCommandHandler.cs
             apps/api/src/.../Features/Attendance/ApplyLeave/ApplyLeaveCommandHandler.cs
             apps/web/app/(dashboard)/teacher/attendance/page.tsx
             apps/web/app/(dashboard)/parent/attendance/page.tsx

FEATURE: Homework Management
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Full approval workflow: Draft → PendingApproval → Published/Rejected.
             CreateHomework, UpdateHomework, SubmitHomeworkForApproval, ApproveHomework, RejectHomework.
             Teacher page supports create/edit/submit/approval actions controlled by canApproveOrReject flag.
             Parent sees published homework. Students/parents see child's class homework via the same endpoint.
             NOTE: Admin homework approval is surfaced on the teacher homework page via canApproveOrReject flag —
             there is no separate /admin/homework route. This is functional but may cause UX confusion.
  Key files: apps/api/src/.../Features/Homework/CreateHomework/CreateHomeworkCommandHandler.cs
             apps/api/src/.../Infrastructure/Database/Migrations/schema/012_add_homework_approval_workflow.sql
             apps/web/app/(dashboard)/teacher/homework/page.tsx
             apps/web/app/(dashboard)/parent/homework/page.tsx

FEATURE: Homework File Attachments (PDF/Word/Images)
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Two-phase upload: request pre-signed S3 URL → upload directly to S3 → attach to entity.
             Supports: image/jpeg, image/png, image/webp, application/pdf, application/msword,
             application/vnd.openxmlformats-officedocument.wordprocessingml.document (.docx).
             Max 10 MB per file. Attachments linked to homework or notices (entity_type).
             AttachmentUploader uses react-dropzone with visual progress feedback.
             AttachmentList renders existing attachments with delete capability.
             Both teacher homework page and parent homework view use attachment components.
  Key files: apps/api/src/.../Features/Attachments/RequestUploadUrlV2/RequestUploadUrlV2CommandHandler.cs
             apps/api/src/.../Infrastructure/Database/Migrations/schema/013_expand_attachment_content_types_for_word_documents.sql
             apps/web/components/shared/attachment-uploader.tsx
             apps/web/components/shared/attachment-list.tsx

FEATURE: Notice Board / Announcements
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Notices support target_audience of All, Class, or Section. Can be linked to a specific class.
             Two-step workflow: CreateNotice (draft) → PublishNotice (live). Expiry date supported.
             Admin can create and publish school-wide or class-specific notices.
             Parent sees notices relevant to their child's class.
             NOTE: No /teacher/notices route exists — teachers cannot post notices directly.
  Key files: apps/api/src/.../Features/Notices/CreateNotice/CreateNoticeCommandHandler.cs
             apps/api/src/.../Features/Notices/PublishNotice/PublishNoticeCommandHandler.cs
             apps/web/app/(dashboard)/admin/notices/page.tsx
             apps/web/app/(dashboard)/parent/notices/page.tsx

FEATURE: In-App Notifications
  Backend:   Complete
  Frontend:  Complete
  Overall:   ⚠️ PARTIAL — Functional polling delivery, NOT real-time push
  Notes:     Backend writes notifications to DB on events: notice_published, homework_assigned,
             absence_marked. NotificationService.SendBatchAsync fans out to all relevant users.
             Endpoints: get paginated list, unread count, mark read, mark all read.
             Frontend NotificationBell polls unread count every 60 seconds. NotificationPanel shows
             paginated list with mark-read actions.
             CRITICAL MISSING: No SignalR, SSE, or WebSocket — delivery is polling only. Users may
             see a notification up to 60s after it is sent. This is functional but not "real-time."
  Key files: apps/api/src/.../Infrastructure/Services/NotificationService.cs
             apps/web/components/shared/notification-bell.tsx
             apps/web/hooks/use-notifications.ts

FEATURE: Student Management
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Enroll, get by ID, list by class, update, deactivate. Parent-student link/unlink.
             Search parents by phone for linking. Frontend covers full CRUD:
             Admin list → new student → student detail → edit student → link/unlink parent.
             Teacher can view students in their class and see individual student detail.
  Key files: apps/api/src/.../Features/Students/EnrollStudent/EnrollStudentCommandHandler.cs
             apps/api/src/.../Features/Students/LinkParentToStudent/LinkParentToStudentCommandHandler.cs
             apps/web/app/(dashboard)/admin/students/page.tsx
             apps/web/app/(dashboard)/admin/students/[id]/link-parent/page.tsx

FEATURE: Teacher Management
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Create teacher, list by school, get profile, assign class+subject, remove assignment,
             promote/demote class teacher flag. Admin detail page lets admin assign/remove/promote
             all in one UI. Teacher profile page shows own assignments.
  Key files: apps/api/src/.../Features/Teachers/CreateTeacher/CreateTeacherCommandHandler.cs
             apps/api/src/.../Features/Teachers/AssignClassToTeacher/AssignClassToTeacherCommandHandler.cs
             apps/web/app/(dashboard)/admin/teachers/page.tsx
             apps/web/app/(dashboard)/admin/teachers/[id]/page.tsx

FEATURE: Parent Portal
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Parents log in with phone+PIN (separate LoginParent endpoint).
             Parent dashboard has 3 sections: Attendance (child's records + leave application),
             Homework (child's published homework with attachments), Notices (school/class notices).
             Parent sees own children via GetStudentsForParent. CreateParent endpoint exists for
             admin to onboard parents.
  Key files: apps/api/src/.../Features/Auth/LoginParent/LoginParentCommandHandler.cs
             apps/api/src/.../Features/Students/GetStudentsForParent/GetStudentsForParentQueryHandler.cs
             apps/web/app/(dashboard)/parent/attendance/page.tsx
             apps/web/app/(dashboard)/parent/homework/page.tsx

FEATURE: Classes Management
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Create, list, and update classes. Each class has name, section, and academic_year.
             Admin classes page supports inline create and edit.
  Key files: apps/api/src/.../Features/Classes/CreateClass/CreateClassCommandHandler.cs
             apps/web/app/(dashboard)/admin/classes/page.tsx

FEATURE: Subjects Management
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Create and list subjects per school. Subjects are used when assigning teachers to classes.
             Admin subjects page supports inline create and displays full catalog.
  Key files: apps/api/src/.../Features/Subjects/CreateSubject/CreateSubjectCommandHandler.cs
             apps/web/app/(dashboard)/admin/subjects/page.tsx

FEATURE: PWA (Progressive Web App)
  Backend:   N/A
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Full PWA implementation — custom (no next-pwa library).
             manifest.json: standalone display, portrait orientation, full icon set (72–512px),
             maskable icons at 192px and 512px, correct theme/background colors.
             sw.js: custom service worker with install/activate/fetch handlers.
             Strategy: network-first for API calls, cache-first for static assets.
             Offline fallback page at /offline. ServiceWorkerRegistrar component registers SW in
             production only. install-prompt.tsx handles Add to Home Screen prompt.
  Key files: apps/web/public/manifest.json
             apps/web/public/sw.js
             apps/web/components/pwa/sw-registrar.tsx
             apps/web/components/pwa/install-prompt.tsx

FEATURE: Rate Limiting
  Backend:   Complete
  Frontend:  N/A
  Overall:   ✅ LIVE
  Notes:     FixedWindowRateLimiter: 60 requests/minute per authenticated userId or remote IP.
             Health endpoint is excluded from limiting. Configurable via RATE_LIMIT_API_PER_USER_PER_MINUTE
             env var. Returns HTTP 429 on breach.
  Key files: apps/api/src/EduConnect.Api/Program.cs (AddRateLimiter / UseRateLimiter blocks)

FEATURE: CORS Configuration
  Backend:   Complete
  Frontend:  N/A
  Overall:   ✅ LIVE
  Notes:     CORS configured via CORS_ALLOWED_ORIGINS env var. Allows credentials (required for
             HttpOnly refresh token cookie). Policy named "AllowConfigured".
  Key files: apps/api/src/EduConnect.Api/Program.cs

FEATURE: CSP / Security Headers
  Backend:   Missing
  Frontend:  Missing
  Overall:   ❌ MISSING
  Notes:     No Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, or other
             security headers configured anywhere in the codebase (backend middleware, next.config.ts,
             or Dockerfile). This is a P1 security gap.

FEATURE: Multi-tenancy / School Isolation
  Backend:   Complete
  Frontend:  N/A
  Overall:   ✅ LIVE
  Notes:     Application-level tenant isolation (not PostgreSQL RLS).
             EF Core global query filters on ALL 15 entities enforce SchoolId matching.
             TenantIsolationMiddleware populates SchoolId into CurrentUserService from JWT claims.
             Filtering is consistent — every entity with a school_id column has a corresponding
             query filter in AppDbContext.OnModelCreating.
  Key files: apps/api/src/.../Infrastructure/Database/AppDbContext.cs
             apps/api/src/.../Common/Middleware/TenantIsolationMiddleware.cs

FEATURE: Bulk Import / Operations
  Backend:   Missing
  Frontend:  Missing
  Overall:   ❌ MISSING
  Notes:     No bulk endpoint, import handler, or import UI exists anywhere in the codebase.

FEATURE: Observability / Error Monitoring
  Backend:   Complete
  Frontend:  Complete
  Overall:   ✅ LIVE
  Notes:     Sentry integration in both backend (Sentry.AspNetCore) and frontend (@sentry/nextjs).
             Activated only when SENTRY_DSN / NEXT_PUBLIC_SENTRY_DSN is set.
             Serilog structured logging to console + rolling JSON files (30-day retention, 50MB cap).
             Correlation ID middleware for request tracing. Sensitive fields auto-redacted in logs.
             Analytics hook present (lib/analytics.ts) but behind feature flag, disabled by default.
  Key files: apps/api/src/EduConnect.Api/Program.cs (Serilog + Sentry config)
             apps/web/sentry.client.config.ts
             apps/web/lib/feature-flags.ts

────────────────
INFRASTRUCTURE
────────────────
Auth provider:     Custom JWT (HS256, symmetric key). Refresh tokens stored in DB (hashed),
                   delivered as HttpOnly cookie. Parents use PIN-based auth; staff use password.
                   Password reset via email (Resend). PIN reset via email.
Database:          PostgreSQL (via DATABASE_URL / Railway Postgres)
ORM:               EF Core 8.0.10 + Npgsql 8.0.10. Raw SQL migration runner (not EF dotnet-ef migrations).
Hosting:           Railway (railway.toml present for both API and web services).
                   Both run in Docker (Dockerfiles present). API uses health check at /health.
                   Web uses standalone Next.js output.
Storage:           AWS S3 / S3-compatible (Cloudflare R2, MinIO supported). Required for file attachments.
                   Configured via S3_SERVICE_URL, S3_ACCESS_KEY, S3_SECRET_KEY, S3_BUCKET_NAME, AWS_REGION.
Email:             Resend (ResendEmailService). Used only for password/PIN reset flows.
Architecture:      Vertical Slice Architecture (VSA) with MediatR + FluentValidation pipeline.
CI/CD:             GitHub Actions (.github/workflows/ci.yml, deploy.yml present).

────────────────
DEPENDENCY INVENTORY
────────────────

Backend (NuGet — EduConnect.Api.csproj):
  MediatR 12.4.0               — CQRS / command-query dispatching
  FluentValidation 11.10.0     — request validation pipeline
  EF Core 8.0.10               — ORM
  Npgsql.EFCore.PostgreSQL 8.0.10 — PostgreSQL provider
  Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10 — JWT validation
  BCrypt.Net-Next 4.0.3        — password / PIN hashing
  System.IdentityModel.Tokens.Jwt 8.0.0 — token generation
  AWSSDK.S3 3.7.402.7          — S3 file storage
  Serilog.AspNetCore 8.0.1     — structured logging
  Sentry.AspNetCore 4.12.1     — error monitoring

Frontend (package.json — apps/web):
  next 15.0.7                  — framework
  react 19.0.0                 — UI runtime
  tailwindcss 4.0.0            — utility CSS
  framer-motion 11.0.0         — animations
  lucide-react 0.408.0         — icons
  react-dropzone 15.0.0        — file upload UX
  class-variance-authority     — component variant management
  @sentry/nextjs 8.40.0        — error monitoring
  State management:            Context API only (no Redux/Zustand)
  Data fetching:               Plain fetch via custom lib/api-client.ts (no SWR/React Query)
  UI component library:        Custom (no shadcn/Radix/MUI — components built from scratch)
  Form library:                None (plain React state + controlled inputs)

────────────────
SUMMARY COUNTS
────────────────
✅ LIVE (complete frontend + backend):      15
⚠️ PARTIAL (one side missing/incomplete):   1
❌ MISSING (not started):                    3

────────────────
CONFIRMED LIVE FEATURES (safe to show clients)
────────────────
✅ Authentication & Login (staff email/password + parent phone/PIN)
✅ Role-Based Access Control (Admin / Teacher / Parent)
✅ Attendance Tracking (mark absent/late, leave application workflow, admin override)
✅ Homework Management (draft → submit → approve/reject → publish workflow)
✅ Homework File Attachments (PDF, Word, images — S3-backed, up to 10 MB)
✅ Notice Board / Announcements (school-wide and class-specific, draft → publish)
✅ Student Management (enroll, edit, deactivate, parent linking)
✅ Teacher Management (create, assign classes/subjects, class teacher promotion)
✅ Parent Portal (attendance + homework + notices for own children)
✅ Classes Management (create, edit, list)
✅ Subjects Management (create, list — used for teacher assignments)
✅ PWA (installable, offline page, custom service worker, full icon set)
✅ Rate Limiting (60 req/min/user, HTTP 429)
✅ CORS Configuration
✅ Multi-tenancy / School Isolation (EF global query filters on all 15 entities)
✅ Observability (Sentry + Serilog structured logs with 30-day retention)

────────────────
IN PROGRESS (do not show as live to clients)
────────────────
⚠️ In-App Notifications
   - Backend (write to DB, get/mark-read endpoints): LIVE
   - Frontend (notification bell + panel + polling): LIVE
   - WHAT'S MISSING: Delivery is 60-second polling only. No real-time push (no SignalR, SSE,
     or WebSocket). Users may wait up to 60s to see a new notification. Functional but
     should NOT be marketed as "real-time notifications."

────────────────
NOT STARTED (roadmap only)
────────────────
❌ Bulk Import / Operations
   - No bulk-enroll, CSV import, or batch-update functionality exists anywhere in the codebase.

❌ CSP / Security Headers
   - No Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, or HSTS headers
     configured in the backend middleware, Next.js config, or Dockerfiles.

❌ Shared API Client Package (packages/api-client)
   - The monorepo has a packages/api-client workspace but its src/generated/ folder contains
     only a .gitkeep. The frontend uses its own lib/api-client.ts directly. The shared package
     was scaffolded but never populated.

────────────────
NOTABLE ISSUES FOUND
────────────────

[P0] JWT Secret committed to .env.local
   - apps/web is in the monorepo and .env.local contains a plaintext JWT_SECRET value.
     If this repo is private this is lower risk, but the secret should be rotated and the
     .env.local file confirmed in .gitignore (it is in .gitignore — verify it was never
     committed in history).

[P1] No Security Headers (CSP / HSTS / X-Frame-Options)
   - Neither the .NET middleware pipeline nor next.config.ts sets any HTTP security headers.
     This means the app is missing XSS framing protection and content-sniffing mitigations.
     Add headers in next.config.ts (headers() config) and in a custom ASP.NET middleware.

[P1] Notification Delivery Is Polling (60s latency)
   - Marketed as "real-time notifications" but is actually polling at 60-second intervals.
     For a production school platform, absence notifications and homework alerts should arrive
     promptly. Recommend adding SSE or SignalR before positioning as real-time.

[P2] No Admin Homework Route
   - Admin role approves homework via canApproveOrReject flag surfaced on the teacher homework
     page. There is no dedicated /admin/homework page. Admins currently have no navigation
     item pointing to homework — they would need to know the URL or it needs to be added to
     the Admin nav.

[P2] No Root Dashboard Pages for Any Role
   - Visiting /admin, /teacher, or /parent redirects to the first nav item (notices/homework/
     attendance). There is no summary/overview dashboard page for any role. Users land directly
     on a feature list page.

[P2] packages/api-client Is Empty / Unused
   - A shared typed API client was scaffolded in packages/api-client but never implemented.
     The frontend duplicates type definitions across lib/types/. This is manageable now but
     will become a drift risk as the API grows.

[P3] No EF Migration Tooling
   - The project uses a custom SQL migration runner (SqlMigrationRunner.cs) rather than
     dotnet-ef migrations. This is intentional (idempotent SQL files) but means there is no
     automatic rollback mechanism. Migrations 002 and 005/006 numbers are missing from the
     sequence (seed files fill 002, but schema 002 and 005/006 are absent), suggesting some
     migrations were renumbered or removed. Confirm the runner handles gaps gracefully.

[P3] No Teacher Notices Capability
   - Teachers have no route or endpoint to post notices. Only Admins can create/publish notices.
     If the product intent is for teachers to post class-level notices, this is a missing feature.

═══════════════════════════════════════════════════
END OF AUDIT REPORT
Generated: 2026-04-15
Auditor: FBT Engineering
═══════════════════════════════════════════════════
