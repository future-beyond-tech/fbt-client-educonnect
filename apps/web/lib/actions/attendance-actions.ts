"use server";

import type { ZodError } from "zod";
import { callBackend, fieldErrorsFromBackend, formErrorFromBackend } from "@/lib/actions/backend";
import {
  applyLeaveSchema,
  rejectLeaveSchema,
  submitAttendanceTakeSchema,
  updateLeaveSchema,
  type ApplyLeaveInput,
  type RejectLeaveInput,
  type SubmitAttendanceTakeInput,
  type UpdateLeaveInput,
} from "@/lib/validation/attendance";

export type ActionResult<T> =
  | { ok: true; data: T }
  | { ok: false; fieldErrors?: Record<string, string>; formError?: string };

interface MessageResponse {
  message: string;
}

interface SubmitAttendanceResponse {
  createdCount: number;
  updatedCount: number;
  clearedCount: number;
  message: string;
}

interface ApplyLeaveResponse {
  leaveApplicationId: string;
  leaveApplicationIds: string[];
  createdCount: number;
  status: string;
  message: string;
}

function zodToFieldErrors(error: ZodError): Record<string, string> {
  const out: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = issue.path[0];
    if (typeof key === "string" && !(key in out)) out[key] = issue.message;
  }
  return out;
}

export async function submitAttendanceTakeAction(
  input: SubmitAttendanceTakeInput,
): Promise<ActionResult<SubmitAttendanceResponse>> {
  const parsed = submitAttendanceTakeSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<SubmitAttendanceResponse>("/api/attendance/take", {
    method: "POST",
    body: JSON.stringify(parsed.data),
  });

  if (!backend.ok) {
    return {
      ok: false,
      fieldErrors: await fieldErrorsFromBackend(backend),
      formError: await formErrorFromBackend(backend),
    };
  }
  return { ok: true, data: backend.data };
}

export async function applyLeaveAction(
  input: ApplyLeaveInput,
): Promise<ActionResult<ApplyLeaveResponse>> {
  const parsed = applyLeaveSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<ApplyLeaveResponse>("/api/leave-applications", {
    method: "POST",
    body: JSON.stringify(parsed.data),
  });

  if (!backend.ok) {
    return {
      ok: false,
      fieldErrors: await fieldErrorsFromBackend(backend),
      formError: await formErrorFromBackend(backend),
    };
  }
  return { ok: true, data: backend.data };
}

export async function updateLeaveAction(
  input: UpdateLeaveInput,
): Promise<ActionResult<MessageResponse>> {
  const parsed = updateLeaveSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<MessageResponse>(
    `/api/leave-applications/${encodeURIComponent(parsed.data.leaveApplicationId)}`,
    {
      method: "PUT",
      body: JSON.stringify({
        startDate: parsed.data.startDate,
        endDate: parsed.data.endDate,
        reason: parsed.data.reason,
      }),
    },
  );

  if (!backend.ok) {
    return {
      ok: false,
      fieldErrors: await fieldErrorsFromBackend(backend),
      formError: await formErrorFromBackend(backend),
    };
  }
  return { ok: true, data: backend.data };
}

export async function approveLeaveAction(
  leaveApplicationId: string,
): Promise<ActionResult<MessageResponse>> {
  const backend = await callBackend<MessageResponse>(
    `/api/leave-applications/${encodeURIComponent(leaveApplicationId)}/approve`,
    { method: "PUT", body: "{}" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: backend.data };
}

export async function rejectLeaveAction(
  input: RejectLeaveInput,
): Promise<ActionResult<MessageResponse>> {
  const parsed = rejectLeaveSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<MessageResponse>(
    `/api/leave-applications/${encodeURIComponent(parsed.data.leaveApplicationId)}/reject`,
    {
      method: "PUT",
      body: JSON.stringify({ reviewNote: parsed.data.reviewNote }),
    },
  );

  if (!backend.ok) {
    return {
      ok: false,
      formError: await formErrorFromBackend(backend),
    };
  }
  return { ok: true, data: backend.data };
}

export async function cancelLeaveAction(
  leaveApplicationId: string,
): Promise<ActionResult<{ ok: true }>> {
  const backend = await callBackend<unknown>(
    `/api/leave-applications/${encodeURIComponent(leaveApplicationId)}`,
    { method: "DELETE" },
  );
  if (!backend.ok) {
    return { ok: false, formError: await formErrorFromBackend(backend) };
  }
  return { ok: true, data: { ok: true } };
}
