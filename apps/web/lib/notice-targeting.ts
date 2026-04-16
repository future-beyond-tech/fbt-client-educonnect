import type { NoticeTargetClassItem } from "@/lib/types/notice";

type NoticeAudienceShape = {
  targetAudience: "All" | "Class" | "Section";
  targetClasses: NoticeTargetClassItem[];
};

type NoticeTargetGroup = {
  key: string;
  className: string;
  academicYear: string;
  sections: string[];
};

function groupTargetClasses(
  targetClasses: NoticeTargetClassItem[]
): NoticeTargetGroup[] {
  const grouped = new Map<string, NoticeTargetGroup>();

  targetClasses.forEach((targetClass) => {
    const key = `${targetClass.className}::${targetClass.academicYear}`;
    const existing = grouped.get(key);

    if (existing) {
      if (!existing.sections.includes(targetClass.section)) {
        existing.sections.push(targetClass.section);
      }
      return;
    }

    grouped.set(key, {
      key,
      className: targetClass.className,
      academicYear: targetClass.academicYear,
      sections: [targetClass.section],
    });
  });

  return Array.from(grouped.values())
    .map((group) => ({
      ...group,
      sections: [...group.sections].sort((left, right) =>
        left.localeCompare(right)
      ),
    }))
    .sort((left, right) => {
      const byName = left.className.localeCompare(right.className);
      if (byName !== 0) {
        return byName;
      }

      return left.academicYear.localeCompare(right.academicYear);
    });
}

function formatClassGroupLabel(group: NoticeTargetGroup): string {
  return group.academicYear
    ? `Class ${group.className} • ${group.academicYear}`
    : `Class ${group.className}`;
}

export function formatNoticeAudienceLabel(
  notice: NoticeAudienceShape
): string {
  if (notice.targetAudience === "All") {
    return "Whole School";
  }

  const groups = groupTargetClasses(notice.targetClasses);
  if (groups.length !== 1) {
    return `${notice.targetClasses.length} Targeted Sections`;
  }

  const [group] = groups;
  if (!group) {
    return notice.targetAudience === "Class" ? "Class Notice" : "Section Notice";
  }

  if (notice.targetAudience === "Class") {
    return `${formatClassGroupLabel(group)} • All Sections`;
  }

  if (group.sections.length === 1) {
    return `${formatClassGroupLabel(group)} • Section ${group.sections[0]}`;
  }

  return `${formatClassGroupLabel(group)} • Sections ${group.sections.join(", ")}`;
}

export function formatNoticeAudienceDetails(
  notice: NoticeAudienceShape
): string | null {
  if (notice.targetAudience === "All") {
    return "Sent to the full school audience.";
  }

  const groups = groupTargetClasses(notice.targetClasses);
  if (groups.length !== 1) {
    return `${notice.targetClasses.length} class sections are targeted.`;
  }

  const [group] = groups;
  if (!group) {
    return null;
  }

  if (notice.targetAudience === "Class") {
    return `${formatClassGroupLabel(group)} will receive this notice across all sections: ${group.sections.join(", ")}.`;
  }

  return `${formatClassGroupLabel(group)} will receive this notice in sections ${group.sections.join(", ")}.`;
}
