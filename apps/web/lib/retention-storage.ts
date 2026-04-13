import type { RoleType } from "@/lib/constants";

const STORAGE_KEY = "educonnect.v1.retention.completed";

type StoredShape = Partial<Record<RoleType, string[]>>;

function parseStored(raw: string): StoredShape {
  try {
    const parsed: unknown = JSON.parse(raw);
    if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
      return {};
    }
    return parsed as StoredShape;
  } catch {
    return {};
  }
}

export function loadRetentionCompleted(role: RoleType): Set<string> {
  if (typeof window === "undefined") {
    return new Set();
  }
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return new Set();
  }
  const data = parseStored(raw);
  return new Set(data[role] ?? []);
}

export function saveRetentionCompleted(
  role: RoleType,
  completedIds: Set<string>
): void {
  if (typeof window === "undefined") {
    return;
  }
  const raw = localStorage.getItem(STORAGE_KEY);
  const data = raw ? parseStored(raw) : {};
  data[role] = Array.from(completedIds);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}
