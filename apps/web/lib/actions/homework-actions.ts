"use server";

import type { ZodError } from "zod";
import { callBackend, fieldErrorsFromBackend, formErrorFromBackend } from "@/lib/actions/backend";
import {
  createHomeworkSchema,
  rejectHomeworkSchema,
  updateHomeworkSchema,
  type CreateHomeworkInput,
  type RejectHomeworkInput,
  type UpdateHomeworkInput,
} from "@/lib/validation/homework";

export type ActionResult<T> =
  | { ok: true; data: T }
  | {
      ok: false;
      fieldErrors?: Record<string, string>;
      formError?: string;
    };

interface CreateHomeworkData {
  homeworkId: string;
  message: string;
}

interface MessageResponse {
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

export async function createHomeworkAction(
  input: CreateHomeworkInput,
): Promise<ActionResult<CreateHomeworkData>> {
  const parsed = createHomeworkSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<CreateHomeworkData>("/api/homework", {
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

export async function updateHomeworkAction(
  input: UpdateHomeworkInput,
): Promise<ActionResult<MessageResponse>> {
  const parsed = updateHomeworkSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<MessageResponse>(
    `/api/homework/${encodeURIComponent(parsed.data.homeworkId)}`,
    {
      method: "PUT",
      body: JSON.stringify(parsed.data),
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

export async function submitHomeworkForApprovalAction(
  homeworkId: string,
): Promise<ActionResult<MessageResponse>> {
  const backend = await callBackend<MessageResponse>(
    `/api/homework/${encodeURIComponent(homeworkId)}/submit`,
    { method: "PUT", body: "{}" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: backend.data };
}

export async function approveHomeworkAction(
  homeworkId: string,
): Promise<ActionResult<MessageResponse>> {
  const backend = await callBackend<MessageResponse>(
    `/api/homework/${encodeURIComponent(homeworkId)}/approve`,
    { method: "PUT", body: "{}" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: backend.data };
}

export async function rejectHomeworkAction(
  input: RejectHomeworkInput,
): Promise<ActionResult<MessageResponse>> {
  const parsed = rejectHomeworkSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<MessageResponse>(
    `/api/homework/${encodeURIComponent(parsed.data.homeworkId)}/reject`,
    {
      method: "PUT",
      body: JSON.stringify({ reason: parsed.data.reason }),
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
