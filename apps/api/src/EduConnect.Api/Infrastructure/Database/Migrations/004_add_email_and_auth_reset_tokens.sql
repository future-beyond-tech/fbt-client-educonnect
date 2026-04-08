-- ============================================================================
-- Migration 004: Add email column to users + auth_reset_tokens table
-- EduConnect — Self-service password & PIN reset
-- Date: 2026-04-08
-- Description: Adds nullable `email` column to users (per-school unique) and
--              creates `auth_reset_tokens` table to support self-service
--              password reset (staff) and PIN reset (parents) via Resend email.
-- ============================================================================

BEGIN;

-- ══════════════════════════════════════════════
-- Add email column to users
-- ══════════════════════════════════════════════
-- Email is optional. Required only for users who want to use self-service
-- password / PIN reset via email. Uniqueness is enforced per-school and is
-- case-insensitive.
ALTER TABLE users ADD COLUMN IF NOT EXISTS email VARCHAR(256);

CREATE UNIQUE INDEX IF NOT EXISTS uix_users_school_email
    ON users(school_id, LOWER(email))
    WHERE email IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_users_email
    ON users(LOWER(email))
    WHERE email IS NOT NULL;

-- ══════════════════════════════════════════════
-- TABLE: auth_reset_tokens
-- ══════════════════════════════════════════════
-- Stores SHA-256 hashes of one-time reset tokens used for password and PIN
-- recovery. Raw tokens are sent via email (Resend) and never persisted.
-- Tokens are single-use: once consumed, used_at is set and the token cannot
-- be reused. Tokens expire after a short window (default 60 minutes).
CREATE TABLE IF NOT EXISTS auth_reset_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      VARCHAR(128) NOT NULL,
    purpose         VARCHAR(20) NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    used_at         TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_auth_reset_purpose CHECK (purpose IN ('Password', 'Pin'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_auth_reset_tokens_token_hash
    ON auth_reset_tokens(token_hash);

CREATE INDEX IF NOT EXISTS idx_auth_reset_tokens_user
    ON auth_reset_tokens(user_id);

CREATE INDEX IF NOT EXISTS idx_auth_reset_tokens_user_purpose_active
    ON auth_reset_tokens(user_id, purpose)
    WHERE used_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_auth_reset_tokens_expires
    ON auth_reset_tokens(expires_at);

-- ══════════════════════════════════════════════
-- MIGRATION TRACKING
-- ══════════════════════════════════════════════
INSERT INTO _migrations (name) VALUES ('004_add_email_and_auth_reset_tokens');

COMMIT;
