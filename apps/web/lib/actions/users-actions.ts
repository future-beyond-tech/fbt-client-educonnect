"use server";

import type { ZodError } from "zod";
import { callBackend, fieldErrorsFromBackend, formErrorFromBackend } from "@/lib/actions/backend";
import {
  assignClassToTeacherSchema,
  createParentSchema,
  createTeacherSchema,
  enrollStudentSchema,
  linkParentSchema,
  updateStudentSchema,
  type AssignClassToTeacherInput,
  type CreateParentInput,
  type CreateTeacherInput,
  type EnrollStudentInput,
  type LinkParentInput,
  type UpdateStudentInput,
} from "@/lib/validation/users";

export type ActionResult<T> =
  | { ok: true; data: T }
  | { ok: false; fieldErrors?: Record<string, string>; formError?: string };

interface MessageResponse {
  message: string;
}

interface CreateTeacherData {
  teacherId: string;
  message: string;
  temporaryPassword: string;
}

interface AssignmentData {
  assignmentId: string;
  message: string;
}

interface EnrollStudentData {
  studentId: string;
  message: string;
  temporaryPin?: string;
}

interface UpdateStudentData {
  studentId: string;
  message: string;
}

interface CreateParentData {
  parentId: string;
  message: string;
  temporaryPin: string;
}

interface LinkParentData {
  linkId: string;
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

// ── Teachers ────────────────────────────────────────────────────────

export async function createTeacherAction(
  input: CreateTeacherInput,
): Promise<ActionResult<CreateTeacherData>> {
  const parsed = createTeacherSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<CreateTeacherData>("/api/teachers", {
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

export async function assignClassToTeacherAction(
  input: AssignClassToTeacherInput,
): Promise<ActionResult<AssignmentData>> {
  const parsed = assignClassToTeacherSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<AssignmentData>(
    `/api/teachers/${encodeURIComponent(parsed.data.teacherId)}/assignments`,
    {
      method: "POST",
      body: JSON.stringify({
        classId: parsed.data.classId,
        subject: parsed.data.subject,
        isClassTeacher: parsed.data.isClassTeacher,
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

export async function removeTeacherAssignmentAction(
  teacherId: string,
  assignmentId: string,
): Promise<ActionResult<{ ok: true }>> {
  const backend = await callBackend<unknown>(
    `/api/teachers/${encodeURIComponent(teacherId)}/assignments/${encodeURIComponent(assignmentId)}`,
    { method: "DELETE" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: { ok: true } };
}

export async function promoteClassTeacherAction(
  teacherId: string,
  assignmentId: string,
): Promise<ActionResult<MessageResponse>> {
  const backend = await callBackend<MessageResponse>(
    `/api/teachers/${encodeURIComponent(teacherId)}/assignments/${encodeURIComponent(assignmentId)}/class-teacher`,
    { method: "PUT", body: "{}" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: backend.data };
}

// ── Students ────────────────────────────────────────────────────────

export async function enrollStudentAction(
  input: EnrollStudentInput,
): Promise<ActionResult<EnrollStudentData>> {
  const parsed = enrollStudentSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  // Map the discriminated union onto the backend's two optional fields.
  const p = parsed.data.parent;
  const payload: Record<string, unknown> = {
    name: parsed.data.name,
    rollNumber: parsed.data.rollNumber,
    classId: parsed.data.classId,
    dateOfBirth: parsed.data.dateOfBirth ?? null,
  };
  if (p.kind === "new") {
    payload.parent = {
      name: p.name,
      phone: p.phone,
      email: p.email,
      pin: p.pin,
      relationship: p.relationship,
    };
  } else if (p.kind === "existing") {
    payload.existingParent = { parentId: p.parentId, relationship: p.relationship };
  }

  const backend = await callBackend<EnrollStudentData>("/api/students", {
    method: "POST",
    body: JSON.stringify(payload),
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

export async function updateStudentAction(
  input: UpdateStudentInput,
): Promise<ActionResult<UpdateStudentData>> {
  const parsed = updateStudentSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<UpdateStudentData>(
    `/api/students/${encodeURIComponent(parsed.data.id)}`,
    {
      method: "PUT",
      body: JSON.stringify({
        name: parsed.data.name,
        classId: parsed.data.classId,
        dateOfBirth: parsed.data.dateOfBirth ?? null,
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

export async function deactivateStudentAction(
  studentId: string,
): Promise<ActionResult<MessageResponse>> {
  const backend = await callBackend<MessageResponse>(
    `/api/students/${encodeURIComponent(studentId)}/deactivate`,
    { method: "PUT", body: "{}" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: backend.data };
}

export async function unlinkParentFromStudentAction(
  studentId: string,
  linkId: string,
): Promise<ActionResult<{ ok: true }>> {
  const backend = await callBackend<unknown>(
    `/api/students/${encodeURIComponent(studentId)}/parent-links/${encodeURIComponent(linkId)}`,
    { method: "DELETE" },
  );
  if (!backend.ok) return { ok: false, formError: await formErrorFromBackend(backend) };
  return { ok: true, data: { ok: true } };
}

// ── Parent links ────────────────────────────────────────────────────

export async function linkParentToStudentAction(
  input: LinkParentInput,
): Promise<ActionResult<LinkParentData>> {
  const parsed = linkParentSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<LinkParentData>(
    `/api/students/${encodeURIComponent(parsed.data.studentId)}/parents`,
    {
      method: "POST",
      body: JSON.stringify({
        parentId: parsed.data.parentId,
        relationship: parsed.data.relationship,
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

export async function createParentAction(
  input: CreateParentInput,
): Promise<ActionResult<CreateParentData>> {
  const parsed = createParentSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false, fieldErrors: zodToFieldErrors(parsed.error) };
  }

  const backend = await callBackend<CreateParentData>("/api/parents", {
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
