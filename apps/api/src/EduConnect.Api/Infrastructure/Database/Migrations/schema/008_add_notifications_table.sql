-- ============================================================================
-- Migration 008: Notifications table
-- Fully idempotent — safe to retry.
-- ============================================================================

CREATE TABLE IF NOT EXISTS notifications (
    id              UUID PRIMARY KEY,
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type            VARCHAR(50) NOT NULL,
    title           VARCHAR(120) NOT NULL,
    body            VARCHAR(500),
    entity_id       UUID,
    entity_type     VARCHAR(50),
    is_read         BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_notification_type CHECK (
        type IN ('notice_published', 'homework_assigned', 'absence_marked')
    ),
    CONSTRAINT chk_notification_entity_type CHECK (
        entity_type IS NULL OR entity_type IN ('notice', 'homework', 'attendance')
    )
);

CREATE INDEX IF NOT EXISTS ix_notifications_user_read_created
    ON notifications (user_id, is_read, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_notifications_school_id
    ON notifications (school_id);
