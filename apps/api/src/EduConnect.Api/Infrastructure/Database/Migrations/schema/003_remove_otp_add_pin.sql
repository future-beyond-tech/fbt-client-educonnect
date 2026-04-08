-- ============================================================================
-- Migration 003: Remove OTP, Add PIN authentication
-- Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

DROP TABLE IF EXISTS otp_records;

ALTER TABLE users ADD COLUMN IF NOT EXISTS pin_hash VARCHAR(500);

COMMIT;
