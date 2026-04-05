-- Migration: 006_add_student_management_columns
-- Date: 2026-04-05
-- Purpose: Add date_of_birth, is_active, created_by to students table.
--          Add relationship column to parent_student_links table.
--          Add composite indexes for student management queries.
-- Policy: Additive only — no column renames, no drops.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM _migrations WHERE name = '006_add_student_management_columns') THEN

        -- ─── students: add date_of_birth (nullable for existing rows) ───
        ALTER TABLE students ADD COLUMN IF NOT EXISTS date_of_birth DATE;

        -- ─── students: add is_active with default true ───
        ALTER TABLE students ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT true;

        -- ─── students: add created_by (nullable for pre-existing seed rows) ───
        ALTER TABLE students ADD COLUMN IF NOT EXISTS created_by UUID;
        ALTER TABLE students ADD CONSTRAINT fk_students_created_by
            FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL;

        -- ─── parent_student_links: add relationship with CHECK constraint ───
        ALTER TABLE parent_student_links ADD COLUMN IF NOT EXISTS relationship VARCHAR(30) NOT NULL DEFAULT 'parent';
        ALTER TABLE parent_student_links ADD CONSTRAINT chk_parent_student_links_relationship
            CHECK (relationship IN ('parent', 'guardian', 'grandparent', 'sibling', 'other'));

        -- ─── Indexes for student management queries ───
        CREATE INDEX IF NOT EXISTS ix_students_school_class_active
            ON students (school_id, class_id, is_active);

        CREATE INDEX IF NOT EXISTS ix_parent_student_links_parent_id
            ON parent_student_links (parent_id);

        -- ─── Record migration ───
        INSERT INTO _migrations (id, name, applied_at)
        VALUES (gen_random_uuid(), '006_add_student_management_columns', now());

    END IF;
END
$$;
