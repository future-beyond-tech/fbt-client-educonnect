-- ============================================================================
-- Seed 004: Fix development parent PIN hashes so PIN "1234" actually works.
-- ⚠ Development environment only.
-- Idempotent: UPDATE is safe to re-run.
-- ============================================================================

BEGIN;

UPDATE users
SET pin_hash = '$2a$12$D1XdfDnisLLwwusuXpP5iOA5SmstDHAMWv1Wt3Z8MgDWJNj11InAG'
WHERE role = 'Parent'
  AND phone IN ('9100000001', '9100000002', '9100000003', '9100000004', '9100000005');

COMMIT;
