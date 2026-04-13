-- ============================================================================
-- Migration 011: Class teacher flag on teacher_class_assignments
-- Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

ALTER TABLE teacher_class_assignments
    ADD COLUMN IF NOT EXISTS is_class_teacher BOOLEAN NOT NULL DEFAULT FALSE;

-- At most one class teacher per school+class
CREATE UNIQUE INDEX IF NOT EXISTS uix_teacher_class_assignments_school_class_class_teacher
    ON teacher_class_assignments (school_id, class_id)
    WHERE is_class_teacher = TRUE;

COMMIT;

