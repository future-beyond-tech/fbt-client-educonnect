import { z } from "zod";

const guidSchema = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, {
    message: "Must be a valid ID.",
  });

const isoDateSchema = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, { message: "Date must be YYYY-MM-DD." });

const attendanceStatusSchema = z.enum([
  "Present",
  "Absent",
  "Late",
  "ExcusedLeave",
]);

export const submitAttendanceTakeSchema = z.object({
  classId: guidSchema,
  date: isoDateSchema,
  items: z
    .array(
      z.object({
        studentId: guidSchema,
        status: attendanceStatusSchema,
        reason: z.string().max(500).nullable().optional(),
      }),
    )
    .min(1, "At least one attendance record is required."),
});
export type SubmitAttendanceTakeInput = z.infer<typeof submitAttendanceTakeSchema>;

export const applyLeaveSchema = z.object({
  studentIds: z.array(guidSchema).min(1, "Select at least one child."),
  startDate: isoDateSchema,
  endDate: isoDateSchema,
  reason: z.string().trim().min(1, "Reason is required.").max(500),
});
export type ApplyLeaveInput = z.infer<typeof applyLeaveSchema>;

export const updateLeaveSchema = z.object({
  leaveApplicationId: guidSchema,
  startDate: isoDateSchema,
  endDate: isoDateSchema,
  reason: z.string().trim().min(1, "Reason is required.").max(500),
});
export type UpdateLeaveInput = z.infer<typeof updateLeaveSchema>;

export const rejectLeaveSchema = z.object({
  leaveApplicationId: guidSchema,
  reviewNote: z.string().trim().min(1, "Review note is required.").max(500),
});
export type RejectLeaveInput = z.infer<typeof rejectLeaveSchema>;

export const leaveIdSchema = z.object({ leaveApplicationId: guidSchema });
