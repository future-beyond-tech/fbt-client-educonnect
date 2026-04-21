import { z } from "zod";

// Matches the .NET CreateHomeworkCommand + UpdateHomeworkCommand shapes.
// Used by both the server action (authoritative) and the client form
// (inline hints).

const guidSchema = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, {
    message: "Must be a valid ID.",
  });

const dueDateSchema = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, { message: "Due date must be YYYY-MM-DD." });

export const createHomeworkSchema = z.object({
  classId: guidSchema,
  subject: z.string().trim().min(1, "Subject is required.").max(100),
  title: z.string().trim().min(1, "Title is required.").max(200),
  description: z.string().trim().min(1, "Description is required.").max(4000),
  dueDate: dueDateSchema,
});
export type CreateHomeworkInput = z.infer<typeof createHomeworkSchema>;

export const updateHomeworkSchema = z.object({
  homeworkId: guidSchema,
  title: z.string().trim().min(1, "Title is required.").max(200),
  description: z.string().trim().min(1, "Description is required.").max(4000),
  dueDate: dueDateSchema,
});
export type UpdateHomeworkInput = z.infer<typeof updateHomeworkSchema>;

export const rejectHomeworkSchema = z.object({
  homeworkId: guidSchema,
  reason: z.string().trim().min(1, "Rejection reason is required.").max(500),
});
export type RejectHomeworkInput = z.infer<typeof rejectHomeworkSchema>;

export const homeworkIdParamSchema = z.object({ homeworkId: guidSchema });
