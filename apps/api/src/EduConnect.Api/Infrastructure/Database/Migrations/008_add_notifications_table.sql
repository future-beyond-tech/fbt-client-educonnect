-- Migration: 008_add_notifications_table
-- Description: Creates notifications table for in-app notification delivery
-- Date: 2026-04-05

CREATE TABLE IF NOT EXISTS notifications (
    id              UUID PRIMARY KEY,
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type            VARCHAR(50) NOT NULL,
    title           VARCHAR(120) NOT NULL,
    body            VARCHAR(500),
    entity_id       UUID,
    entity_type     VARCHAR(50),
    is_read         BOOLEAN NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

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

INSERT INTO _migrations (name, applied_at)
VALUES ('008_add_notifications_table', now());
