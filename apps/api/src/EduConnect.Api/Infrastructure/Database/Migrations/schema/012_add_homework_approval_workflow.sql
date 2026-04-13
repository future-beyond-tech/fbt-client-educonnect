-- ============================================================================
-- Migration 012: Homework approval workflow fields
-- Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

ALTER TABLE homework
    ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'Draft',
    ADD COLUMN IF NOT EXISTS submitted_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS approved_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS approved_by_id UUID NULL,
    ADD COLUMN IF NOT EXISTS rejected_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS rejected_by_id UUID NULL,
    ADD COLUMN IF NOT EXISTS rejected_reason VARCHAR(500) NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_homework_status'
    ) THEN
        ALTER TABLE homework
            ADD CONSTRAINT chk_homework_status
            CHECK (status IN ('Draft', 'PendingApproval', 'Published', 'Rejected'));
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_homework_approved_by_id_users'
    ) THEN
        ALTER TABLE homework
            ADD CONSTRAINT fk_homework_approved_by_id_users
            FOREIGN KEY (approved_by_id) REFERENCES users(id) ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_homework_rejected_by_id_users'
    ) THEN
        ALTER TABLE homework
            ADD CONSTRAINT fk_homework_rejected_by_id_users
            FOREIGN KEY (rejected_by_id) REFERENCES users(id) ON DELETE SET NULL;
    END IF;
END $$;

-- Backfill: treat existing published homework as Published
UPDATE homework
SET status = 'Published'
WHERE published_at IS NOT NULL
  AND status = 'Draft';

COMMIT;

