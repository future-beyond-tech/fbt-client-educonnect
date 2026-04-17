# EduConnect — Current Capabilities (by role)

This document lists the **currently implemented** capabilities in the EduConnect repo, grouped by **who can do what**, and **how** (where in the UI + which API endpoints are used).

Source-of-truth references:
- **Web routes**: `apps/web/app/**/page.tsx`
- **API endpoints**: `apps/api/src/EduConnect.Api/Common/Extensions/EndpointRouteBuilderExtensions.cs`
- **Navigation by role**: `apps/web/lib/constants.ts`

> Notes
> - Roles visible in the current web app: **Admin**, **Teacher**, **Parent**.
> - All API endpoints are under `/api/*`. Auth endpoints include anonymous endpoints; most other groups require authorization.

---

## Global / shared (all authenticated users)

- **Session-based access to role dashboard**
  - **How**: After login, user is routed to `defaultRouteByRole[role]`.
  - **Web**: `apps/web/app/(auth)/login/login-form.tsx`, `apps/web/lib/constants.ts`

- **Dashboard shell (header/sidebar + retention slot)**
  - **How**: Any `/admin/*`, `/teacher/*`, `/parent/*` route uses the shared dashboard layout.
  - **Web route**: `apps/web/app/(dashboard)/layout.tsx`

- **Notifications**
  - **What**:
    - View notifications list (loaded when the panel opens)
    - See unread count badge
    - Mark one notification as read
    - Mark all notifications as read
    - Navigate from notification to the related page (currently mapped to Parent pages)
  - **How (UI)**:
    - Click bell icon → opens panel → click notification row to mark read + navigate.
  - **Web**: `apps/web/components/shared/notification-bell.tsx`, `apps/web/components/shared/notification-panel.tsx`
  - **API**:
    - `GET /api/notifications`
    - `GET /api/notifications/unread-count`
    - `PUT /api/notifications/{id}/read`
    - `PUT /api/notifications/read-all`

- **Attachments (file upload + attach to entity)**
  - **What**:
    - Upload files (JPEG/PNG/WebP/PDF, max 10MB, up to max attachments per entity)
    - Attach uploaded file to an entity
    - List attachments for an entity
    - Delete an attachment
  - **How (UI)**:
    - For supported entities (currently **Notice** and **Homework**), after creating the draft item the UI displays an attachment uploader, plus attachment list on cards.
  - **Web**:
    - Uploader: `apps/web/components/shared/attachment-uploader.tsx`
    - Viewer list: `apps/web/components/shared/attachment-list.tsx`
  - **API**:
    - `POST /api/attachments/request-upload-url`
    - `POST /api/attachments/attach`
    - `GET /api/attachments` (query by entity in request)
    - `DELETE /api/attachments/{id}`

---

## Auth (login / recovery)

### Login

- **Parent login (phone + PIN)**
  - **How**: `/login` → “I’m a Parent” → enter *Phone* and *PIN* → Login.
  - **Web**: `apps/web/app/(auth)/login/login-form.tsx`
  - **API**: `POST /api/auth/login-parent`
  - **After login**: the parent session can load every linked child through `GET /api/students/my-children`

- **Staff login (phone + password)**
  - **How**: `/login` → “I’m Staff” → enter *Phone* and *Password* → Login.
  - **Web**: `apps/web/app/(auth)/login/login-form.tsx`
  - **API**: `POST /api/auth/login`

### Password reset (staff)

- **Request password reset email**
  - **How**: `/forgot-password` → enter email → Send reset link.
  - **Web**: `apps/web/app/(auth)/forgot-password/forgot-password-form.tsx`
  - **API**: `POST /api/auth/forgot-password`

- **Reset password (token-based)**
  - **How**: `/reset-password?token=...` → set new password → submit.
  - **Web**: `apps/web/app/(auth)/reset-password/reset-password-form.tsx`
  - **API**: `POST /api/auth/reset-password`

### PIN reset (parent)

- **Request PIN reset email**
  - **How**: `/forgot-pin` → enter email → Send reset link.
  - **Web**: `apps/web/app/(auth)/forgot-pin/forgot-pin-form.tsx`
  - **API**: `POST /api/auth/forgot-pin`

- **Reset PIN (token-based)**
  - **How**: `/reset-pin?token=...` → set new PIN → submit.
  - **Web**: `apps/web/app/(auth)/reset-pin/reset-pin-form.tsx`
  - **API**: `POST /api/auth/reset-pin`

### Other auth endpoints (API exists; UI depends on app flows)

- **Set parent PIN**
  - **API**: `PUT /api/auth/set-pin`
- **Refresh token**
  - **API**: `POST /api/auth/refresh`
- **Logout**
  - **API**: `POST /api/auth/logout`

---

## Admin capabilities

### Admin navigation (current)

- **Notices**: `/admin/notices`
- **Students**: `/admin/students`
- **Teachers**: `/admin/teachers`

Source: `apps/web/lib/constants.ts`

### Notices (Admin)

- **View notices (draft + published)**
  - **How**: Open `/admin/notices` → page loads notices → shows Drafts + Published groups.
  - **Web route**: `apps/web/app/(dashboard)/admin/notices/page.tsx`
  - **API**: `GET /api/notices`

- **Create notice (draft) with audience targeting**
  - **What**: title, body, target audience (All/Class/Section), targetClassId (if not All), optional expiry.
  - **How**: `/admin/notices` → “New Notice” → fill form → “Create Draft”.
  - **API**: `POST /api/notices`

- **Attach files to a draft notice**
  - **How**: After draft create, the page shows “Attach Files to Notice” (uploader) for the new noticeId.
  - **API**:
    - `POST /api/attachments/request-upload-url`
    - `POST /api/attachments/attach`
    - `GET /api/attachments`
    - `DELETE /api/attachments/{id}`

- **Publish notice**
  - **How**: In Drafts list → click “Publish”.
  - **API**: `PUT /api/notices/{id}/publish`

### Students (Admin)

- **List students (filter by class, search, pagination)**
  - **How**: `/admin/students` → use search + class filters (provided by `StudentListPage` / `useStudentList`).
  - **Web route**: `apps/web/app/(dashboard)/admin/students/page.tsx`
  - **API (used by the list hook/page)**:
    - `GET /api/students` (students by class)
    - `GET /api/classes` (class filter options)

- **Enroll new student**
  - **How**: `/admin/students/new` → enter name, roll number, class, optional DOB, and optionally skip parent setup, link an existing parent account, or create a new parent account during enrollment.
  - **Web route**: `apps/web/app/(dashboard)/admin/students/new/page.tsx`
  - **API**:
    - `GET /api/classes`
    - `POST /api/students`

- **View student details**
  - **How**: `/admin/students/{id}` shows profile + linked parents.
  - **Web route**: `apps/web/app/(dashboard)/admin/students/[id]/page.tsx`
  - **API**: `GET /api/students/{id}`

- **Edit student (name, class, DOB)**
  - **How**: `/admin/students/{id}/edit` → edit fields → Save.
  - **Web route**: `apps/web/app/(dashboard)/admin/students/[id]/edit/page.tsx`
  - **API**:
    - `GET /api/students/{id}`
    - `GET /api/classes`
    - `PUT /api/students/{id}`

- **Deactivate student**
  - **How**: `/admin/students/{id}` → “Deactivate” → confirm.
  - **API**: `PUT /api/students/{id}/deactivate`

- **Link parent to student (search by phone, choose relationship)**
  - **How**: `/admin/students/{id}/link-parent`
    - Search by phone (`min 3 digits`)
    - Select a parent result
    - Choose relationship (parent/guardian/grandparent/sibling/other)
    - Link
  - **Web route**: `apps/web/app/(dashboard)/admin/students/[id]/link-parent/page.tsx`
  - **API**:
    - `GET /api/students/search-parents?phone=...`
    - `POST /api/students/{id}/parent-links`

- **Unlink parent from student**
  - **How**: `/admin/students/{id}` → linked parents list → unlink → confirm.
  - **Web route**: `apps/web/app/(dashboard)/admin/students/[id]/page.tsx`
  - **API**: `DELETE /api/students/{id}/parent-links/{linkId}`

### Teachers (Admin)

- **List/search teachers + paginate**
  - **How**: `/admin/teachers` → search by name/phone → page through results.
  - **Web route**: `apps/web/app/(dashboard)/admin/teachers/page.tsx`
  - **API**: `GET /api/teachers?search=&page=&pageSize=`

- **View teacher profile + assignments**
  - **How**: `/admin/teachers/{id}` → shows profile + current assignments.
  - **Web route**: `apps/web/app/(dashboard)/admin/teachers/[id]/page.tsx`
  - **API**:
    - `GET /api/teachers/{id}`
    - `GET /api/classes`
    - `GET /api/subjects`

- **Assign a class + subject to a teacher**
  - **How**: `/admin/teachers/{id}` → “Assign” → choose class + subject → Assign.
  - **API**: `POST /api/teachers/{id}/assignments`

- **Remove a class assignment from a teacher**
  - **How**: `/admin/teachers/{id}` → trash icon next to assignment → confirm.
  - **API**: `DELETE /api/teachers/{id}/assignments/{assignmentId}`

### Subjects (Admin)

- **View subject catalog**
  - **How**: `/admin/subjects`
  - **Web route**: `apps/web/app/(dashboard)/admin/subjects/page.tsx`
  - **API**: `GET /api/subjects`

- **Create a subject**
  - **How**: `/admin/subjects` → “Add Subject” → enter name → Create.
  - **API**: `POST /api/subjects`

---

## Teacher capabilities

### Teacher navigation (current)

- **Homework**: `/teacher/homework`
- **Attendance**: `/teacher/attendance`
- **Students**: `/teacher/students`
- **Profile**: `/teacher/profile`

Source: `apps/web/lib/constants.ts`

### Homework (Teacher)

- **View homework you created / assigned**
  - **How**: `/teacher/homework` loads a list of homework items.
  - **Web route**: `apps/web/app/(dashboard)/teacher/homework/page.tsx`
  - **API**: `GET /api/homework`

- **Create homework**
  - **What**: classId, subject, title, description, dueDate.
  - **How**: `/teacher/homework` → “New Homework” → fill form → Create.
  - **API**: `POST /api/homework`

- **Attach files to homework**
  - **How**: After create, uploader is shown for the new homeworkId.
  - **API**:
    - `POST /api/attachments/request-upload-url`
    - `POST /api/attachments/attach`
    - `GET /api/attachments`
    - `DELETE /api/attachments/{id}`

- **Edit homework (if `isEditable`)**
  - **How**: `/teacher/homework` → click pencil icon → edit title/description/dueDate → Save.
  - **API**: `PUT /api/homework/{id}`

### Attendance (Teacher)

- **View attendance records for a month/year**
  - **How**: `/teacher/attendance` → select month/year → records list.
  - **Web route**: `apps/web/app/(dashboard)/teacher/attendance/page.tsx`
  - **API**: `GET /api/attendance?month=&year=`

- **Mark a student absent**
  - **How**: `/teacher/attendance` → “Mark Absence” → enter Student ID + date + optional reason → Submit.
  - **API**: `POST /api/attendance`

### Students (Teacher)

- **List students (filter by class, search, pagination)**
  - **How**: `/teacher/students` uses the same list component/hook pattern as admin, but presented as “View students in your assigned classes.”
  - **Web route**: `apps/web/app/(dashboard)/teacher/students/page.tsx`
  - **API (used by the list hook/page)**:
    - `GET /api/students`
    - `GET /api/classes`

- **View student details**
  - **How**: `/teacher/students/{id}`
  - **Web route**: `apps/web/app/(dashboard)/teacher/students/[id]/page.tsx`
  - **API**: `GET /api/students/{id}`

### Profile (Teacher)

- **View “my classes” assignments**
  - **How**: `/teacher/profile` → loads assignments and groups by class.
  - **Web route**: `apps/web/app/(dashboard)/teacher/profile/page.tsx`
  - **API**: `GET /api/teachers/my-classes`

---

## Parent capabilities

### Parent navigation (current)

- **Attendance**: `/parent/attendance`
- **Homework**: `/parent/homework`
- **Notices**: `/parent/notices`

Source: `apps/web/lib/constants.ts`

### Attendance (Parent)

- **View absence records by month/year**
  - **How**: `/parent/attendance` → “Absence Records” tab → select month/year.
  - **Web route**: `apps/web/app/(dashboard)/parent/attendance/page.tsx`
  - **API**: `GET /api/attendance?month=&year=`

- **Apply for leave (for a linked child)**
  - **How**: `/parent/attendance` → “Apply Leave” → slide-over form:
    - Loads linked children
    - Choose child, start date, end date, reason
    - Submit
  - **API**:
    - `GET /api/students/my-children` (child picker)
    - `POST /api/attendance/leave`

- **View leave application history**
  - **How**: `/parent/attendance` → “Leave Applications” tab (fetches on tab open).
  - **API**: `GET /api/attendance/leave`

### Homework (Parent)

- **View homework**
  - **How**: `/parent/homework` loads homework list.
  - **Web route**: `apps/web/app/(dashboard)/parent/homework/page.tsx`
  - **API**: `GET /api/homework`

- **Filter homework by subject**
  - **How**: `/parent/homework` → subject pills (derived from homework items) → refetch with subject query.
  - **API**: `GET /api/homework?subject=...`

- **View homework attachments**
  - **How**: In each homework card, attachments are listed.
  - **API**: `GET /api/attachments` (entity: homework)

### Notices (Parent)

- **View notices**
  - **How**: `/parent/notices` loads notices; click a notice card to expand/collapse body + attachments.
  - **Web route**: `apps/web/app/(dashboard)/parent/notices/page.tsx`
  - **API**: `GET /api/notices`

- **View notice attachments**
  - **How**: attachments appear in expanded notice view.
  - **API**: `GET /api/attachments` (entity: notice)

---

## API surface area (complete list from current endpoint mapping)

This is the full list of mapped endpoints in the current API application.

### Auth
- `POST /api/auth/login` (anonymous)
- `POST /api/auth/login-parent` (anonymous)
- `PUT /api/auth/set-pin` (authorized)
- `POST /api/auth/refresh` (anonymous)
- `POST /api/auth/logout` (authorized)
- `POST /api/auth/forgot-password` (anonymous)
- `POST /api/auth/reset-password` (anonymous)
- `POST /api/auth/forgot-pin` (anonymous)
- `POST /api/auth/reset-pin` (anonymous)

### Attendance
- `POST /api/attendance` (authorized) — mark absence
- `GET /api/attendance` (authorized) — get attendance
- `PUT /api/attendance/{recordId}/override` (authorized) — admin override
- `POST /api/attendance/leave` (authorized) — apply leave
- `GET /api/attendance/leave` (authorized) — get leave applications

### Homework
- `POST /api/homework` (authorized) — create homework
- `GET /api/homework` (authorized) — get homework
- `PUT /api/homework/{id}` (authorized) — update homework

### Notices
- `POST /api/notices` (authorized) — create notice
- `GET /api/notices` (authorized) — get notices
- `PUT /api/notices/{id}/publish` (authorized) — publish notice

### Students
- `GET /api/students` (authorized) — get students by class
- `GET /api/students/my-children` (authorized) — get students for parent
- `GET /api/students/search-parents` (authorized) — search parents by phone
- `GET /api/students/{id}` (authorized) — student detail
- `POST /api/students` (authorized) — enroll student
- `PUT /api/students/{id}` (authorized) — update student
- `PUT /api/students/{id}/deactivate` (authorized) — deactivate
- `POST /api/students/{id}/parent-links` (authorized) — link parent
- `DELETE /api/students/{id}/parent-links/{linkId}` (authorized) — unlink parent

### Classes
- `GET /api/classes` (authorized) — classes by school

### Teachers
- `GET /api/teachers` (authorized) — teachers by school
- `GET /api/teachers/my-classes` (authorized) — classes for teacher
- `GET /api/teachers/{id}` (authorized) — teacher profile
- `POST /api/teachers/{id}/assignments` (authorized) — assign class/subject
- `DELETE /api/teachers/{id}/assignments/{assignmentId}` (authorized) — remove assignment

### Subjects
- `GET /api/subjects` (authorized) — subjects by school
- `POST /api/subjects` (authorized) — create subject

### Notifications
- `GET /api/notifications` (authorized)
- `GET /api/notifications/unread-count` (authorized)
- `PUT /api/notifications/{id}/read` (authorized)
- `PUT /api/notifications/read-all` (authorized)

### Attachments
- `POST /api/attachments/request-upload-url` (authorized)
- `POST /api/attachments/attach` (authorized)
- `GET /api/attachments` (authorized)
- `DELETE /api/attachments/{id}` (authorized)
