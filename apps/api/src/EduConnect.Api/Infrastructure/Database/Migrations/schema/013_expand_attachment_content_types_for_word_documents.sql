-- ============================================================================
-- Migration 013: Expand attachment content types to support Word documents
-- Fully idempotent — safe to retry.
-- ============================================================================

ALTER TABLE attachments
    DROP CONSTRAINT IF EXISTS chk_attachment_content_type;

ALTER TABLE attachments
    ADD CONSTRAINT chk_attachment_content_type CHECK (
        content_type IN (
            'image/jpeg',
            'image/png',
            'image/webp',
            'application/pdf',
            'application/msword',
            'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
        )
    );
