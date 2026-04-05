-- Migration: 007_add_subjects_and_teacher_management
-- Date: 2026-04-05
-- Purpose: Add subjects reference table for school-level subject CRUD.
--          Add assigned_by column to teacher_class_assignments for audit trail.
-- Policy: Additive only — no column renames, no drops.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM _migrations WHERE name = '007_add_subjects_and_teacher_management') THEN

        -- ─── subjects: reference table for valid subject names per school ───
        CREATE TABLE IF NOT EXISTS subjects (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            school_id UUID NOT NULL REFERENCES schools(id) ON DELETE CASCADE,
            name VARCHAR(80) NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE(school_id, name)
        );

        CREATE INDEX IF NOT EXISTS ix_subjects_school_id ON subjects (school_id);

        -- ─── teacher_class_assignments: add assigned_by for audit trail ───
        ALTER TABLE teacher_class_assignments ADD COLUMN IF NOT EXISTS assigned_by UUID;
        ALTER TABLE teacher_class_assignments ADD CONSTRAINT fk_teacher_class_assignments_assigned_by
            FOREIGN KEY (assigned_by) REFERENCES users(id) ON DELETE SET NULL;

        -- ─── Seed subjects for existing development data ───
        -- The seed data has teachers assigned to Mathematics, English, Science
        INSERT INTO subjects (id, school_id, name, created_at)
        SELECT gen_random_uuid(), s.id, subj.name, now()
        FROM schools s
        CROSS JOIN (VALUES ('Mathematics'), ('English'), ('Science')) AS subj(name)
        WHERE s.code = 'SRIS-CHN'
        ON CONFLICT (school_id, name) DO NOTHING;

        -- ─── Record migration ───
        INSERT INTO _migrations (id, name, applied_at)
        VALUES (gen_random_uuid(), '007_add_subjects_and_teacher_management', now());

    END IF;
END
$$;
