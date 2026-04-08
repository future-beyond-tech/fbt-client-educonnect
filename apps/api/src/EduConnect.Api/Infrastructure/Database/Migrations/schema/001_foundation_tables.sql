-- ============================================================================
-- Migration 001: Foundation Tables
-- EduConnect — Product Genesis
-- Description: Creates all foundation tables for EduConnect MVP.
--              Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

-- ══════════════════════════════════════════════
-- EXTENSIONS
-- ══════════════════════════════════════════════
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ══════════════════════════════════════════════
-- TABLE: schools (tenant root)
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS schools (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(200) NOT NULL,
    code            VARCHAR(20) NOT NULL,
    address         TEXT,
    contact_phone   VARCHAR(20),
    contact_email   VARCHAR(200),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_schools_code ON schools(code);

-- ══════════════════════════════════════════════
-- TABLE: users (parents, teachers, admins)
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    phone           VARCHAR(20) NOT NULL,
    name            VARCHAR(200) NOT NULL,
    role            VARCHAR(20) NOT NULL,
    password_hash   VARCHAR(500),
    pin_hash        VARCHAR(500),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_users_role CHECK (role IN ('Parent', 'Teacher', 'Admin'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_users_school_phone ON users(school_id, phone);
CREATE INDEX IF NOT EXISTS idx_users_school_id ON users(school_id);
CREATE INDEX IF NOT EXISTS idx_users_phone ON users(phone);

-- ══════════════════════════════════════════════
-- TABLE: classes
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS classes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    name            VARCHAR(50) NOT NULL,
    section         VARCHAR(10),
    academic_year   VARCHAR(10) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_classes_school_name_section_year
    ON classes(school_id, name, section, academic_year);
CREATE INDEX IF NOT EXISTS idx_classes_school_id ON classes(school_id);

-- ══════════════════════════════════════════════
-- TABLE: teacher_class_assignments
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS teacher_class_assignments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    teacher_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    class_id        UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    subject         VARCHAR(100) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_tca_teacher_class_subject
    ON teacher_class_assignments(teacher_id, class_id, subject);
CREATE INDEX IF NOT EXISTS idx_tca_school_id ON teacher_class_assignments(school_id);
CREATE INDEX IF NOT EXISTS idx_tca_teacher_id ON teacher_class_assignments(teacher_id);
CREATE INDEX IF NOT EXISTS idx_tca_class_id ON teacher_class_assignments(class_id);

-- ══════════════════════════════════════════════
-- TABLE: students
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS students (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    class_id        UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    roll_number     VARCHAR(20) NOT NULL,
    name            VARCHAR(200) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_students_school_class_roll
    ON students(school_id, class_id, roll_number);
CREATE INDEX IF NOT EXISTS idx_students_school_id ON students(school_id);
CREATE INDEX IF NOT EXISTS idx_students_class_id ON students(class_id);

-- ══════════════════════════════════════════════
-- TABLE: parent_student_links
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS parent_student_links (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    parent_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    student_id      UUID NOT NULL REFERENCES students(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_psl_parent_student
    ON parent_student_links(parent_id, student_id);
CREATE INDEX IF NOT EXISTS idx_psl_school_id ON parent_student_links(school_id);
CREATE INDEX IF NOT EXISTS idx_psl_parent_id ON parent_student_links(parent_id);
CREATE INDEX IF NOT EXISTS idx_psl_student_id ON parent_student_links(student_id);

-- ══════════════════════════════════════════════
-- TABLE: attendance_records
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS attendance_records (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    student_id      UUID NOT NULL REFERENCES students(id) ON DELETE CASCADE,
    date            DATE NOT NULL,
    status          VARCHAR(20) NOT NULL,
    reason          TEXT,
    entered_by_id   UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    entered_by_role VARCHAR(20) NOT NULL,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_attendance_status CHECK (status IN ('Absent', 'Late')),
    CONSTRAINT chk_attendance_entered_by_role CHECK (entered_by_role IN ('Parent', 'Admin'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_attendance_student_date_active
    ON attendance_records(student_id, date)
    WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_attendance_school_id ON attendance_records(school_id);
CREATE INDEX IF NOT EXISTS idx_attendance_student_id ON attendance_records(student_id);
CREATE INDEX IF NOT EXISTS idx_attendance_school_student_date
    ON attendance_records(school_id, student_id, date);
CREATE INDEX IF NOT EXISTS idx_attendance_entered_by
    ON attendance_records(entered_by_id);

-- ══════════════════════════════════════════════
-- TABLE: homework
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS homework (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    class_id        UUID NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    subject         VARCHAR(100) NOT NULL,
    title           VARCHAR(300) NOT NULL,
    description     TEXT NOT NULL,
    assigned_by_id  UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    due_date        DATE,
    published_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_editable     BOOLEAN NOT NULL DEFAULT TRUE,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_homework_school_id ON homework(school_id);
CREATE INDEX IF NOT EXISTS idx_homework_class_id ON homework(class_id);
CREATE INDEX IF NOT EXISTS idx_homework_assigned_by ON homework(assigned_by_id);
CREATE INDEX IF NOT EXISTS idx_homework_due_date ON homework(due_date);
CREATE INDEX IF NOT EXISTS idx_homework_class_not_deleted
    ON homework(class_id, is_deleted)
    WHERE is_deleted = FALSE;

-- ══════════════════════════════════════════════
-- TABLE: notices
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS notices (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    title           VARCHAR(300) NOT NULL,
    body            TEXT NOT NULL,
    target_audience VARCHAR(50) NOT NULL,
    target_class_id UUID REFERENCES classes(id) ON DELETE SET NULL,
    published_by_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    is_published    BOOLEAN NOT NULL DEFAULT FALSE,
    published_at    TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_notices_target_audience CHECK (target_audience IN ('All', 'Class', 'Section'))
);

CREATE INDEX IF NOT EXISTS idx_notices_school_id ON notices(school_id);
CREATE INDEX IF NOT EXISTS idx_notices_published_by ON notices(published_by_id);
CREATE INDEX IF NOT EXISTS idx_notices_target_class ON notices(target_class_id);
CREATE INDEX IF NOT EXISTS idx_notices_school_published
    ON notices(school_id, is_published, is_deleted)
    WHERE is_published = TRUE AND is_deleted = FALSE;

-- ══════════════════════════════════════════════
-- TABLE: refresh_tokens
-- ══════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      VARCHAR(500) NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    is_revoked      BOOLEAN NOT NULL DEFAULT FALSE,
    revoked_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    replaced_by_id  UUID REFERENCES refresh_tokens(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user ON refresh_tokens(user_id);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_active
    ON refresh_tokens(user_id, is_revoked)
    WHERE is_revoked = FALSE;
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires ON refresh_tokens(expires_at);

COMMIT;
