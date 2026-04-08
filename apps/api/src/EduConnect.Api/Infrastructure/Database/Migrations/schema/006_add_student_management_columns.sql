-- ============================================================================
-- Migration 006: Student management columns
-- Adds date_of_birth, is_active, created_by to students.
-- Adds relationship column to parent_student_links.
-- Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

-- ─── students ──────────────────────────────────
ALTER TABLE students ADD COLUMN IF NOT EXISTS date_of_birth DATE;
ALTER TABLE students ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE students ADD COLUMN IF NOT EXISTS created_by UUID;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_students_created_by'
    ) THEN
        ALTER TABLE students ADD CONSTRAINT fk_students_created_by
            FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL;
    END IF;
END $$;

-- ─── parent_student_links ──────────────────────
ALTER TABLE parent_student_links
    ADD COLUMN IF NOT EXISTS relationship VARCHAR(30) NOT NULL DEFAULT 'parent';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_parent_student_links_relationship'
    ) THEN
        ALTER TABLE parent_student_links ADD CONSTRAINT chk_parent_student_links_relationship
            CHECK (relationship IN ('parent', 'guardian', 'grandparent', 'sibling', 'other'));
    END IF;
END $$;

-- ─── Indexes ───────────────────────────────────
CREATE INDEX IF NOT EXISTS ix_students_school_class_active
    ON students (school_id, class_id, is_active);

CREATE INDEX IF NOT EXISTS ix_parent_student_links_parent_id
    ON parent_student_links (parent_id);

COMMIT;
