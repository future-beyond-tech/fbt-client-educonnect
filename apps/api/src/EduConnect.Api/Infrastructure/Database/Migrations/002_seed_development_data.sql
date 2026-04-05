-- ============================================================================
-- Migration 002: Seed Development Data
-- EduConnect — Development Environment Only
-- Date: 2026-04-03
-- Description: Seeds 1 school, 1 admin, 2 teachers, 3 classes, 10 students,
--              5 parents with parent-student links and teacher-class assignments.
--
-- ⚠ DO NOT RUN IN PRODUCTION — development seed data only.
--
-- Password for teachers/admin: "EduConnect@2026"
-- BCrypt hash (work factor 12) of "EduConnect@2026"
-- Parents use PIN only — no password_hash needed.
-- Development PIN for all parents: "1234"
-- PIN hash (BCrypt, work factor 12) is placeholder for dev seeding.
-- ============================================================================

BEGIN;

-- ══════════════════════════════════════════════
-- SCHOOL
-- ══════════════════════════════════════════════
INSERT INTO schools (id, name, code, address, contact_phone, contact_email)
VALUES (
    'a1b2c3d4-0001-4000-8000-000000000001',
    'Sunrise International School',
    'SRIS-CHN',
    '42 Anna Nagar, Chennai, Tamil Nadu 600040',
    '+919876543210',
    'admin@sunriseschool.edu.in'
);

-- ══════════════════════════════════════════════
-- ADMIN USER
-- Password: EduConnect@2026
-- BCrypt hash generated with work factor 12
-- ══════════════════════════════════════════════
INSERT INTO users (id, school_id, phone, name, role, password_hash)
VALUES (
    'b1b2c3d4-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9000000001',
    'Rajesh Kumar',
    'Admin',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
);

-- ══════════════════════════════════════════════
-- TEACHER USERS
-- Password: EduConnect@2026
-- ══════════════════════════════════════════════
INSERT INTO users (id, school_id, phone, name, role, password_hash)
VALUES
(
    'b1b2c3d4-0002-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9000000002',
    'Priya Sharma',
    'Teacher',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
),
(
    'b1b2c3d4-0003-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9000000003',
    'Anand Venkatesh',
    'Teacher',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
);

-- ══════════════════════════════════════════════
-- PARENT USERS (PIN-based auth)
-- Development PIN: "1234" (BCrypt hash below is a dev placeholder)
-- ══════════════════════════════════════════════
INSERT INTO users (id, school_id, phone, name, role, pin_hash)
VALUES
(
    'b1b2c3d4-0010-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9100000001',
    'Meena Devi',
    'Parent',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
),
(
    'b1b2c3d4-0011-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9100000002',
    'Suresh Babu',
    'Parent',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
),
(
    'b1b2c3d4-0012-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9100000003',
    'Lakshmi Narayan',
    'Parent',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
),
(
    'b1b2c3d4-0013-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9100000004',
    'Karthik Rajan',
    'Parent',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
),
(
    'b1b2c3d4-0014-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '9100000005',
    'Deepa Sundar',
    'Parent',
    '$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa'
);

-- ══════════════════════════════════════════════
-- CLASSES (3 classes for academic year 2026-27)
-- ══════════════════════════════════════════════
INSERT INTO classes (id, school_id, name, section, academic_year)
VALUES
(
    'c1c2c3d4-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '5',
    'A',
    '2026-27'
),
(
    'c1c2c3d4-0002-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '5',
    'B',
    '2026-27'
),
(
    'c1c2c3d4-0003-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    '6',
    'A',
    '2026-27'
);

-- ══════════════════════════════════════════════
-- TEACHER ↔ CLASS ASSIGNMENTS
-- Priya Sharma: teaches Maths in 5A, English in 5B
-- Anand Venkatesh: teaches Science in 5A, Maths in 6A
-- ══════════════════════════════════════════════
INSERT INTO teacher_class_assignments (id, school_id, teacher_id, class_id, subject)
VALUES
(
    'd1d2d3d4-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0002-4000-8000-000000000001',
    'c1c2c3d4-0001-4000-8000-000000000001',
    'Mathematics'
),
(
    'd1d2d3d4-0002-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0002-4000-8000-000000000001',
    'c1c2c3d4-0002-4000-8000-000000000001',
    'English'
),
(
    'd1d2d3d4-0003-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0003-4000-8000-000000000001',
    'c1c2c3d4-0001-4000-8000-000000000001',
    'Science'
),
(
    'd1d2d3d4-0004-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0003-4000-8000-000000000001',
    'c1c2c3d4-0003-4000-8000-000000000001',
    'Mathematics'
);

-- ══════════════════════════════════════════════
-- STUDENTS (10 total: 4 in 5A, 3 in 5B, 3 in 6A)
-- ══════════════════════════════════════════════
INSERT INTO students (id, school_id, class_id, roll_number, name)
VALUES
-- Class 5A (4 students)
(
    'e1e2e3e4-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0001-4000-8000-000000000001',
    '5A-001',
    'Arjun Meena'
),
(
    'e1e2e3e4-0002-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0001-4000-8000-000000000001',
    '5A-002',
    'Kavitha Suresh'
),
(
    'e1e2e3e4-0003-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0001-4000-8000-000000000001',
    '5A-003',
    'Ravi Lakshmi'
),
(
    'e1e2e3e4-0004-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0001-4000-8000-000000000001',
    '5A-004',
    'Divya Karthik'
),
-- Class 5B (3 students)
(
    'e1e2e3e4-0005-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0002-4000-8000-000000000001',
    '5B-001',
    'Arun Rajan'
),
(
    'e1e2e3e4-0006-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0002-4000-8000-000000000001',
    '5B-002',
    'Sneha Deepa'
),
(
    'e1e2e3e4-0007-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0002-4000-8000-000000000001',
    '5B-003',
    'Vikram Anand'
),
-- Class 6A (3 students)
(
    'e1e2e3e4-0008-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0003-4000-8000-000000000001',
    '6A-001',
    'Nithya Venkat'
),
(
    'e1e2e3e4-0009-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0003-4000-8000-000000000001',
    '6A-002',
    'Sanjay Kumar'
),
(
    'e1e2e3e4-0010-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'c1c2c3d4-0003-4000-8000-000000000001',
    '6A-003',
    'Pooja Narayan'
);

-- ══════════════════════════════════════════════
-- PARENT ↔ STUDENT LINKS
-- Meena Devi → Arjun Meena (5A) + Kavitha Suresh (5A) [2 children]
-- Suresh Babu → Kavitha Suresh (5A) [co-parent]
-- Lakshmi Narayan → Ravi Lakshmi (5A) + Pooja Narayan (6A) [2 children, diff classes]
-- Karthik Rajan → Divya Karthik (5A)
-- Deepa Sundar → Sneha Deepa (5B) + Arun Rajan (5B)
-- ══════════════════════════════════════════════
INSERT INTO parent_student_links (id, school_id, parent_id, student_id)
VALUES
-- Meena Devi → Arjun + Kavitha
(
    'f1f2f3f4-0001-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0010-4000-8000-000000000001',
    'e1e2e3e4-0001-4000-8000-000000000001'
),
(
    'f1f2f3f4-0002-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0010-4000-8000-000000000001',
    'e1e2e3e4-0002-4000-8000-000000000001'
),
-- Suresh Babu → Kavitha (co-parent)
(
    'f1f2f3f4-0003-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0011-4000-8000-000000000001',
    'e1e2e3e4-0002-4000-8000-000000000001'
),
-- Lakshmi Narayan → Ravi (5A) + Pooja (6A)
(
    'f1f2f3f4-0004-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0012-4000-8000-000000000001',
    'e1e2e3e4-0003-4000-8000-000000000001'
),
(
    'f1f2f3f4-0005-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0012-4000-8000-000000000001',
    'e1e2e3e4-0010-4000-8000-000000000001'
),
-- Karthik Rajan → Divya (5A)
(
    'f1f2f3f4-0006-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0013-4000-8000-000000000001',
    'e1e2e3e4-0004-4000-8000-000000000001'
),
-- Deepa Sundar → Sneha (5B) + Arun (5B)
(
    'f1f2f3f4-0007-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0014-4000-8000-000000000001',
    'e1e2e3e4-0006-4000-8000-000000000001'
),
(
    'f1f2f3f4-0008-4000-8000-000000000001',
    'a1b2c3d4-0001-4000-8000-000000000001',
    'b1b2c3d4-0014-4000-8000-000000000001',
    'e1e2e3e4-0005-4000-8000-000000000001'
);

INSERT INTO _migrations (name) VALUES ('002_seed_development_data');

COMMIT;
