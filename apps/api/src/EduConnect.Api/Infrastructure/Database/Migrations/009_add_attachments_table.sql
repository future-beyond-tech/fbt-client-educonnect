-- Migration: 009_add_attachments_table
-- Description: Creates attachments table for file uploads on homework and notices
-- Date: 2026-04-05

CREATE TABLE IF NOT EXISTS attachments (
    id              UUID PRIMARY KEY,
    school_id       UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
    entity_id       UUID,
    entity_type     VARCHAR(30),
    storage_key     VARCHAR(500) NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    content_type    VARCHAR(100) NOT NULL,
    size_bytes      INT NOT NULL,
    uploaded_by     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT chk_attachment_entity_type CHECK (
        entity_type IS NULL OR entity_type IN ('homework', 'notice')
    ),
    CONSTRAINT chk_attachment_content_type CHECK (
        content_type IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')
    ),
    CONSTRAINT chk_attachment_size CHECK (size_bytes > 0 AND size_bytes <= 10485760)
);

CREATE INDEX IF NOT EXISTS ix_attachments_entity
    ON attachments (entity_id, entity_type);

CREATE INDEX IF NOT EXISTS ix_attachments_school_id
    ON attachments (school_id);

CREATE INDEX IF NOT EXISTS ix_attachments_uploaded_by
    ON attachments (uploaded_by);

-- Index for orphan cleanup: unattached uploads older than 24h
CREATE INDEX IF NOT EXISTS ix_attachments_orphan_cleanup
    ON attachments (uploaded_at) WHERE entity_id IS NULL;

INSERT INTO _migrations (name, applied_at)
VALUES ('009_add_attachments_table', now());
