-- ============================================================================
-- Migration 005: Fix Development Staff Password Hashes
-- EduConnect — Development Data Repair
-- Date: 2026-04-05
-- Description: Updates development admin/teacher accounts so their seeded
--              password matches the documented value of "EduConnect@2026".
--
-- ⚠ DO NOT RUN IN PRODUCTION — development seed data only.
-- ============================================================================

BEGIN;

UPDATE users
SET password_hash = '$2a$12$RVblikCT9RnEoUu6bo8aA.E8pO7BSdWSEE07EKGG2EtX2NFyos6i.'
WHERE role IN ('Admin', 'Teacher')
  AND phone IN ('9000000001', '9000000002', '9000000003');

INSERT INTO _migrations (name) VALUES ('005_fix_development_staff_password_hashes');

COMMIT;
