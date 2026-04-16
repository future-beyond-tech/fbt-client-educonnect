-- ============================================================================
-- Seed 004: Fix development parent PIN hashes so PIN "1234" actually works.
-- ⚠ Development environment only.
-- Idempotent: UPDATE is safe to re-run.
-- ============================================================================

BEGIN;

UPDATE users
SET pin_hash = '$2a$12$D1XdfDnisLLwwusuXpP5iOA5SmstDHAMWv1Wt3Z8MgDWJNj11InAG'
WHERE role = 'Parent'
  AND phone IN ('09100000001', '09100000002', '09100000003', '09100000004', '09100000005');

COMMIT;
