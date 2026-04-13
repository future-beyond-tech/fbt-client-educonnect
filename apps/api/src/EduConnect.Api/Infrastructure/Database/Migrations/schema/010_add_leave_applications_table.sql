-- Migration: 010_add_leave_applications_table
-- Purpose : Tracks leave applications submitted by parents on behalf of students.
--           Leave is modelled as a separate lifecycle entity from attendance_records
--           because: (a) it spans multiple days, (b) it has an approval workflow.

CREATE TABLE IF NOT EXISTS leave_applications (
    id              UUID        NOT NULL,
    school_id       UUID        NOT NULL REFERENCES schools(id)   ON DELETE CASCADE,
    student_id      UUID        NOT NULL REFERENCES students(id)  ON DELETE CASCADE,
    parent_id       UUID        NOT NULL REFERENCES users(id)     ON DELETE RESTRICT,
    start_date      DATE        NOT NULL,
    end_date        DATE        NOT NULL,
    reason          TEXT        NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'Pending',
    reviewed_by_id  UUID                 REFERENCES users(id)     ON DELETE SET NULL,
    reviewed_at     TIMESTAMPTZ,
    review_note     TEXT,
    is_deleted      BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_leave_applications         PRIMARY KEY (id),
    CONSTRAINT chk_leave_applications_status CHECK (status   IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT chk_leave_applications_dates  CHECK (end_date >= start_date)
);

-- Indexes
CREATE INDEX IF NOT EXISTS ix_leave_applications_school_id   ON leave_applications (school_id);
CREATE INDEX IF NOT EXISTS ix_leave_applications_student_id  ON leave_applications (student_id);
CREATE INDEX IF NOT EXISTS ix_leave_applications_parent_id   ON leave_applications (parent_id);
CREATE INDEX IF NOT EXISTS ix_leave_applications_school_status
    ON leave_applications (school_id, status) WHERE is_deleted = FALSE;
