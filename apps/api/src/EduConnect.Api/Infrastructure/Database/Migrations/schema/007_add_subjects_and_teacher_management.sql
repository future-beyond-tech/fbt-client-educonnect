-- ============================================================================
-- Migration 007: Subjects table + assigned_by on teacher_class_assignments
-- Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

-- ─── subjects ──────────────────────────────────
CREATE TABLE IF NOT EXISTS subjects (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id   UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    name        VARCHAR(80) NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(school_id, name)
);

CREATE INDEX IF NOT EXISTS ix_subjects_school_id ON subjects (school_id);

-- ─── teacher_class_assignments ─────────────────
ALTER TABLE teacher_class_assignments
    ADD COLUMN IF NOT EXISTS assigned_by UUID;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_teacher_class_assignments_assigned_by'
    ) THEN
        ALTER TABLE teacher_class_assignments ADD CONSTRAINT fk_teacher_class_assignments_assigned_by
            FOREIGN KEY (assigned_by) REFERENCES users(id) ON DELETE SET NULL;
    END IF;
END $$;

COMMIT;
