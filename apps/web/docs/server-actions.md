# Server Actions migration — pattern for Phase 7 waves 2–5

Wave 1 (auth) is merged. This document is the template every subsequent
wave follows so reviewers see a repeating shape across
homework / attendance / notices / user-admin PRs.

## Why Server Actions

- **CSRF** — Next 15 encrypts the action payload so cross-origin replay is
  rejected by the framework itself.
- **Progressive enhancement** — forms work without JS when the action
  runs on the Next.js server.
- **Less hand-rolled loading / error glue** — `useActionState` +
  `useFormStatus` replace the `isSubmitting` / `error` state every form
  was previously re-implementing.
- **Stack alignment** — the FBT stack doc declares Server Actions as the
  mutation path.

## The per-action shape

Every action:

```ts
"use server";

import { z } from "zod";
import { callBackend } from "@/lib/actions/backend";

const schema = z.object({ /* same schema as the form's client-side parse */ });

type Input = z.infer<typeof schema>;
type Success = { /* DTO */ };

export type Result =
  | { ok: true; data: Success }
  | { ok: false; fieldErrors?: Partial<Record<keyof Input, string>>; formError?: string };

export async function createHomeworkAction(
  _prev: Result | null,
  formData: FormData,
): Promise<Result> {
  const parsed = schema.safeParse(Object.fromEntries(formData));
  if (!parsed.success) { /* map issues to fieldErrors */ }

  const result = await callBackend<Success>("/api/homework", {
    method: "POST",
    body: JSON.stringify(parsed.data),
  });
  if (!result.ok) { /* map backend errors */ }
  return { ok: true, data: result.data };
}
```

Forms use `useActionState`:

```tsx
"use client";
const [state, formAction, isPending] = useActionState(createHomeworkAction, null);

return (
  <form action={formAction}>
    {/* fields */}
    <button disabled={isPending}>{isPending ? "Saving…" : "Create"}</button>
    {state && !state.ok && state.formError && <Error>{state.formError}</Error>}
  </form>
);
```

Rules:
- Input is parsed by the same Zod schema the client used to render
  inline validation hints. Keep both under `lib/validation/*`.
- The return type is **always** the `Result` discriminated union. No
  throwing for expected errors; only unexpected exceptions bubble to
  the error boundary.
- Do not call `redirect()` on error paths. Return the error shape and
  let the form render it.

## Auth token flow in a Server Action

The in-memory `tokenStore` (Phase 3) lives in the browser and is
inaccessible from server code. Each action that hits the .NET API calls
`mintBackendAccessToken()` from `lib/actions/auth-actions.ts` to mint a
per-request bearer using the browser's HttpOnly refresh cookie, then
forwards the rotated cookie back via `cookies().set()`. The call helper
(forthcoming, to land in Wave 2) wraps this:

```ts
// lib/actions/backend.ts  -- scaffold that Wave 2 will create
"use server";

import { mintBackendAccessToken } from "@/lib/actions/auth-actions";

export async function callBackend<T>(
  path: string,
  init: RequestInit = {},
): Promise<{ ok: true; data: T } | { ok: false; status: number; problem?: unknown }> {
  const token = await mintBackendAccessToken();
  if (!token) return { ok: false, status: 401 };

  const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init.headers ?? {}),
      Authorization: `Bearer ${token}`,
    },
  });

  if (!res.ok) {
    const problem = await res.json().catch(() => undefined);
    return { ok: false, status: res.status, problem };
  }

  return { ok: true, data: res.status === 204 ? (undefined as T) : ((await res.json()) as T) };
}
```

## Migration waves and order

Each wave is **one branch, one PR**.

| Wave | Domain | Scope |
|------|--------|-------|
| 1    | Auth   | login, logout + shared `mintBackendAccessToken` helper. **Done.** |
| 2    | Homework | create, update, submit-for-approval, approve, reject. Also lands the `callBackend` helper. |
| 3    | Attendance | mark, bulk mark, edit, leave apply/approve/reject/cancel. |
| 4    | Notices | create, publish, pin, delete. |
| 5    | User CRUD | create-teacher, link-parent-to-student, enroll-student, deactivate-student. |

Read endpoints stay as-is — no dashboard pages need to move off the
browser-side `api-client`. Server Actions are mutation-only.

## Known limitations (addressed in later waves)

### Concurrent mint → refresh-token reuse false positive

Two Server Actions that fire within the same tick both present the
same refresh cookie to `/api/auth/refresh`. The Phase 3 reuse-detection
path revokes the family and logs the user out.

Current status: NOT HIT in practice because waves 2-5 haven't landed
yet. When a wave starts triggering it, the fix is a backend-side
non-rotating mint (e.g. a query parameter that mints a fresh access
token without revoking the presented refresh token). That change
belongs to Wave 2 so it ships alongside the first action that needs it.

### Rate limiter keying

The .NET rate limiter partitions on `userId` claim (see `Program.cs`).
The Server Action's bearer carries the same claim so this continues to
work, but the rate limit now accounts for the per-action mint call
(one extra `/auth/refresh` per mutation). Re-check partition sizing
when Wave 3 (attendance bulk mark) ships.

### Reads stay on the client

`api-client.ts` continues to serve every GET. Waves 2-5 only move
mutations. This means each page still bootstraps auth via the existing
in-memory `tokenStore` + single-flight refresh from Phase 3. The two
systems coexist — actions handle mutations, client handles reads.
