-- ============================================================================
-- Migration 004: Fix Development Parent PIN Hashes
-- EduConnect — Development Data Repair
-- Date: 2026-04-04
-- Description: Updates development parent accounts so their seeded PIN matches
--              the documented value of "1234".
--
-- ⚠ DO NOT RUN IN PRODUCTION — development seed data only.
-- ============================================================================

BEGIN;

UPDATE users
SET pin_hash = '$2a$12$D1XdfDnisLLwwusuXpP5iOA5SmstDHAMWv1Wt3Z8MgDWJNj11InAG'
WHERE role = 'Parent'
  AND phone IN ('9100000001', '9100000002', '9100000003', '9100000004', '9100000005');

INSERT INTO _migrations (name) VALUES ('004_fix_development_parent_pin_hashes');

COMMIT;
