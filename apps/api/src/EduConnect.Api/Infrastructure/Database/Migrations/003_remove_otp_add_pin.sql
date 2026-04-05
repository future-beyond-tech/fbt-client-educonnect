-- ============================================================================
-- Migration 003: Remove OTP, Add PIN authentication
-- EduConnect — Auth Refactor
-- Date: 2026-04-04
-- Description: Replaces OTP-based parent auth with Phone + PIN (4-6 digit).
--              Drops otp_records table, adds pin_hash column to users.
--              Parents authenticate via Phone + PIN. Teachers/Admins via Phone + Password.
-- ============================================================================

BEGIN;

-- ══════════════════════════════════════════════
-- Drop OTP table (no longer needed)
-- ══════════════════════════════════════════════
DROP TABLE IF EXISTS otp_records;

-- ══════════════════════════════════════════════
-- Add PIN hash column to users
-- ══════════════════════════════════════════════
-- pin_hash is VARCHAR(500) nullable — only parents use PIN.
-- Teachers and admins continue using password_hash.
ALTER TABLE users ADD COLUMN IF NOT EXISTS pin_hash VARCHAR(500);

-- ══════════════════════════════════════════════
-- MIGRATION TRACKING
-- ══════════════════════════════════════════════
INSERT INTO _migrations (name) VALUES ('003_remove_otp_add_pin');

COMMIT;
