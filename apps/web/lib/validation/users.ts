import { z } from "zod";
import { passwordSchema } from "@/lib/validation/password";

const guidSchema = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, {
    message: "Must be a valid ID.",
  });

const japanPhoneSchema = z
  .string()
  .regex(/^\d{11}$/, { message: "Phone number must be exactly 11 digits." });

const emailSchema = z.string().trim().toLowerCase().email("Enter a valid email address.").max(256);

const pinSchema = z.string().regex(/^\d{4,6}$/, "PIN must be 4 to 6 digits.");

const isoDateOnlySchema = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, { message: "Date must be YYYY-MM-DD." });

// ── Teachers ────────────────────────────────────────────────────────

export const createTeacherSchema = z
  .object({
    name: z.string().trim().min(1, "Name is required.").max(200),
    phone: japanPhoneSchema,
    email: emailSchema,
    password: passwordSchema,
    role: z.enum(["Teacher", "Admin"]).default("Teacher"),
    classId: guidSchema.optional(),
    subject: z.string().trim().max(100).optional(),
    isClassTeacher: z.boolean().optional(),
  })
  .refine(
    (v) => v.role !== "Teacher" || (Boolean(v.classId) === Boolean(v.subject && v.subject.trim().length > 0)),
    {
      message: "Provide both a class and a subject, or leave both blank.",
      path: ["subject"],
    },
  );
export type CreateTeacherInput = z.infer<typeof createTeacherSchema>;

export const assignClassToTeacherSchema = z.object({
  teacherId: guidSchema,
  classId: guidSchema,
  subject: z.string().trim().min(1, "Subject is required.").max(100),
  isClassTeacher: z.boolean().default(false),
});
export type AssignClassToTeacherInput = z.infer<typeof assignClassToTeacherSchema>;

// ── Students ────────────────────────────────────────────────────────

const enrollParentNewSchema = z.object({
  kind: z.literal("new"),
  name: z.string().trim().min(1, "Parent name is required.").max(200),
  phone: japanPhoneSchema,
  email: emailSchema,
  pin: pinSchema,
  relationship: z.string().trim().max(64).default("parent"),
});

const enrollParentExistingSchema = z.object({
  kind: z.literal("existing"),
  parentId: guidSchema,
  relationship: z.string().trim().max(64).default("parent"),
});

const enrollParentNoneSchema = z.object({ kind: z.literal("none") });

export const enrollStudentSchema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  rollNumber: z.string().trim().min(1, "Roll number is required.").max(50),
  classId: guidSchema,
  dateOfBirth: isoDateOnlySchema.nullable().optional(),
  parent: z
    .discriminatedUnion("kind", [
      enrollParentNewSchema,
      enrollParentExistingSchema,
      enrollParentNoneSchema,
    ])
    .default({ kind: "none" }),
});
export type EnrollStudentInput = z.infer<typeof enrollStudentSchema>;

export const updateStudentSchema = z.object({
  id: guidSchema,
  name: z.string().trim().min(1, "Name is required.").max(200),
  classId: guidSchema,
  dateOfBirth: isoDateOnlySchema.nullable().optional(),
});
export type UpdateStudentInput = z.infer<typeof updateStudentSchema>;

// ── Parent linking ──────────────────────────────────────────────────

export const linkParentSchema = z.object({
  studentId: guidSchema,
  parentId: guidSchema,
  relationship: z.string().trim().max(64).default("parent"),
});
export type LinkParentInput = z.infer<typeof linkParentSchema>;

export const createParentSchema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  phone: japanPhoneSchema,
  email: emailSchema,
  pin: pinSchema,
});
export type CreateParentInput = z.infer<typeof createParentSchema>;
