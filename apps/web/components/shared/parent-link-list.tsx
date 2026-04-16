"use client";

import * as React from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Unlink } from "lucide-react";
import type { ParentLink } from "@/lib/types/student";

export interface ParentLinkListProps {
  links: ParentLink[];
  canUnlink?: boolean;
  onUnlink?: (linkId: string, parentName: string) => void;
  isUnlinking?: string | null;
}

export function ParentLinkList({
  links,
  canUnlink = false,
  onUnlink,
  isUnlinking = null,
}: ParentLinkListProps): React.ReactElement {
  if (links.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        No parents linked to this student.
      </p>
    );
  }

  return (
    <ul className="space-y-3" aria-label="Linked parents">
      {links.map((link) => (
        <li
          key={link.linkId}
          className="flex items-center justify-between gap-4 rounded-[24px] border border-border/70 bg-card/72 p-4 shadow-[0_16px_36px_-30px_rgba(15,40,69,0.42)] backdrop-blur-sm dark:bg-card/86"
        >
          <div className="min-w-0 flex-1">
            <p className="font-semibold text-foreground">{link.parentName}</p>
            <p className="text-sm text-muted-foreground">{link.parentPhone}</p>
            {link.parentEmail && (
              <p className="text-xs text-muted-foreground">{link.parentEmail}</p>
            )}
          </div>
          <div className="ml-3 flex shrink-0 items-center gap-2">
            <Badge variant="outline" className="capitalize">
              {link.relationship}
            </Badge>
            {canUnlink && onUnlink && (
              <Button
                variant="ghost"
                size="icon"
                onClick={() => onUnlink(link.linkId, link.parentName)}
                disabled={isUnlinking === link.linkId}
                aria-label={`Unlink ${link.parentName}`}
                className="h-8 w-8 text-destructive hover:text-destructive"
              >
                <Unlink className="h-4 w-4" />
              </Button>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}
