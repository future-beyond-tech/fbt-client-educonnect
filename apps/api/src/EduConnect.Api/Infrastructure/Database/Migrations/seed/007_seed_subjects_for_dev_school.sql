-- ============================================================================
-- Seed 007: Seed the three default subjects for the development school.
-- ⚠ Development environment only (keyed off the SRIS-CHN school code).
-- Idempotent: ON CONFLICT (school_id, name) DO NOTHING.
-- ============================================================================

BEGIN;

INSERT INTO subjects (id, school_id, name, created_at)
SELECT gen_random_uuid(), s.id, subj.name, NOW()
FROM schools s
CROSS JOIN (VALUES ('Mathematics'), ('English'), ('Science')) AS subj(name)
WHERE s.code = 'SRIS-CHN'
ON CONFLICT (school_id, name) DO NOTHING;

COMMIT;
