-- ============================================================================
-- Migration 004: Add email column to users + auth_reset_tokens table
-- Fully idempotent — safe to retry.
-- ============================================================================

BEGIN;

ALTER TABLE users ADD COLUMN IF NOT EXISTS email VARCHAR(256);

CREATE UNIQUE INDEX IF NOT EXISTS uix_users_school_email
    ON users(school_id, LOWER(email))
    WHERE email IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_users_email
    ON users(LOWER(email))
    WHERE email IS NOT NULL;

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

COMMIT;
