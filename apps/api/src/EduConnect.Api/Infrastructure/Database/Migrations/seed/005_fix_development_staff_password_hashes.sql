-- ============================================================================
-- Seed 005: Fix development staff password hashes so "EduConnect@2026" works.
-- ⚠ Development environment only.
-- Idempotent: UPDATE is safe to re-run.
-- ============================================================================

BEGIN;

UPDATE users
SET password_hash = '$2a$12$RVblikCT9RnEoUu6bo8aA.E8pO7BSdWSEE07EKGG2EtX2NFyos6i.'
WHERE role IN ('Admin', 'Teacher')
  AND phone IN ('9000000001', '9000000002', '9000000003');

COMMIT;
